using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

public enum RuntVehicleQueryErrorCode
{
    MissingPlate,
    VehicleNotFound,
    AllProvidersUnavailable,
}

public sealed record RuntVehicleQueryError(
    RuntVehicleQueryErrorCode Code,
    string Message,
    bool RadicationBlocked);

public static class QueryVehicleWithRuntContingency
{
    public const string Operation = "vehicle_lookup";

    public sealed record Command(
        Guid TenantId,
        string Plate,
        Guid? ProcedureInstanceId,
        Guid ActorUserId,
        bool SimulateRuntUnavailable,
        bool SimulateAllProvidersDown);

    public sealed record Response(
        string ProviderUsed,
        string? FailoverFrom,
        string ResultJson,
        Guid SyncLogId,
        string Outcome,
        bool RadicationBlocked,
        Guid? ProcedureQueryResultId);

    public static async Task<Result<Response, RuntVehicleQueryError>> HandleAsync(
        Command cmd,
        ICompanyModuleConfigsRepository configRepo,
        IEnumerable<IRuntVehicleQueryProvider> providers,
        IRuntSyncLogRepository syncLogRepo,
        IProcedureQueryResultsRepository queryResultsRepo,
        IRuntProviderCircuitBreaker circuitBreaker,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Plate))
        {
            return Result<Response, RuntVehicleQueryError>.Failure(
                new RuntVehicleQueryError(
                    RuntVehicleQueryErrorCode.MissingPlate,
                    "plate es requerida.",
                    RadicationBlocked: false));
        }

        var plate = cmd.Plate.Trim().ToUpperInvariant();
        var now = clock.UtcNow;

        var configRow = await configRepo.GetByTenantAndModuleAsync(
            cmd.TenantId,
            CompanyModuleKey.RuntContingency,
            ct);
        var policy = configRow is { IsActive: true }
            ? RuntContingencyPolicy.Parse(configRow.ConfigJson)
            : RuntContingencyPolicy.Default;

        var providerMap = providers.ToDictionary(p => p.ProviderCode, StringComparer.Ordinal);
        var chain = policy.BuildAttemptChain();

        var request = new RuntVehicleQueryRequest(
            cmd.TenantId,
            plate,
            cmd.SimulateRuntUnavailable,
            cmd.SimulateAllProvidersDown);

        string? previousProvider = null;
        var anyAttempt = false;
        var onlyNotFoundFailures = true;

        foreach (var providerCode in chain)
        {
            if (!providerMap.TryGetValue(providerCode, out var provider))
                continue;

            anyAttempt = true;

            if (cmd.SimulateRuntUnavailable && providerCode == RuntProviderCode.Runt)
            {
                await AppendLogAsync(syncLogRepo, cmd.TenantId, providerCode, "failed", previousProvider, plate, now, ct);
                circuitBreaker.RecordFailure(cmd.TenantId, providerCode, now);
                onlyNotFoundFailures = false;
                previousProvider = providerCode;
                continue;
            }

            if (circuitBreaker.IsOpen(cmd.TenantId, providerCode, now))
            {
                await AppendLogAsync(
                    syncLogRepo,
                    cmd.TenantId,
                    providerCode,
                    "circuit_open",
                    previousProvider,
                    plate,
                    now,
                    ct);
                onlyNotFoundFailures = false;
                continue;
            }

            var attempt = await provider.QueryVehicleAsync(request, ct);

            if (attempt.Succeeded)
            {
                circuitBreaker.RecordSuccess(cmd.TenantId, providerCode);
                var failoverFrom = previousProvider;
                var payload = failoverFrom is null
                    ? attempt.ResultJson
                    : AppendFailoverReason(attempt.ResultJson, failoverFrom);

                var successLog = RuntSyncLogEntry.Create(
                    cmd.TenantId,
                    providerCode,
                    Operation,
                    RuntQueryOutcome.Ok,
                    failoverFrom,
                    payload,
                    now);
                await syncLogRepo.AddAsync(successLog, ct);

                Guid? queryResultId = null;
                if (cmd.ProcedureInstanceId is { } instanceId && instanceId != Guid.Empty)
                {
                    var snapshot = ProcedureQueryResultSnapshot.CreateOk(
                        cmd.TenantId,
                        instanceId,
                        providerCode,
                        attempt.ResultJson,
                        cmd.ActorUserId,
                        now);
                    await queryResultsRepo.AddAsync(snapshot, ct);
                    queryResultId = snapshot.Id;
                }

                await saveChanges(ct);

                return Result<Response, RuntVehicleQueryError>.Success(
                    new Response(
                        providerCode,
                        failoverFrom,
                        attempt.ResultJson,
                        successLog.Id,
                        RuntQueryOutcome.Success,
                        RadicationBlocked: false,
                        queryResultId));
            }

            var syncOutcome = MapAttemptOutcomeToSyncLog(attempt.Outcome);
            await AppendLogAsync(
                syncLogRepo,
                cmd.TenantId,
                providerCode,
                syncOutcome,
                previousProvider,
                plate,
                now,
                ct,
                attempt.ResultJson);

            if (attempt.Outcome == RuntQueryOutcome.NotFound)
            {
                previousProvider = providerCode;
                continue;
            }

            onlyNotFoundFailures = false;
            circuitBreaker.RecordFailure(cmd.TenantId, providerCode, now);
            previousProvider = providerCode;
        }

        var lastProvider = chain.Count > 0 ? chain[^1] : policy.Primary;
        var exhaustedLog = RuntSyncLogEntry.Create(
            cmd.TenantId,
            lastProvider,
            Operation,
            "circuit_open",
            previousProvider,
            $$"""{"plate":"{{plate}}","reason":"all_providers_exhausted"}""",
            now);
        await syncLogRepo.AddAsync(exhaustedLog, ct);
        await saveChanges(ct);

        if (anyAttempt && onlyNotFoundFailures)
        {
            return Result<Response, RuntVehicleQueryError>.Failure(
                new RuntVehicleQueryError(
                    RuntVehicleQueryErrorCode.VehicleNotFound,
                    "Vehículo no encontrado en los proveedores de consulta.",
                    RadicationBlocked: false));
        }

        return Result<Response, RuntVehicleQueryError>.Failure(
            new RuntVehicleQueryError(
                RuntVehicleQueryErrorCode.AllProvidersUnavailable,
                "Todos los proveedores de consulta vehicular están indisponibles. La radicación puede continuar sin bloqueo.",
                RadicationBlocked: false));
    }

    private static string MapAttemptOutcomeToSyncLog(string attemptOutcome) =>
        attemptOutcome switch
        {
            RuntQueryOutcome.NotFound => RuntQueryOutcome.Failed,
            RuntQueryOutcome.Ok or RuntQueryOutcome.Success => RuntQueryOutcome.Ok,
            _ => attemptOutcome,
        };

    private static string AppendFailoverReason(string resultJson, string failoverFrom)
    {
        if (string.IsNullOrWhiteSpace(resultJson) || resultJson == "{}")
        {
            return $$"""{"reason":"failover","failover_from":"{{failoverFrom}}"}""";
        }

        if (resultJson.EndsWith('}'))
        {
            return resultJson[..^1] + $$""","reason":"failover","failover_from":"{{failoverFrom}}"}""";
        }

        return resultJson;
    }

    private static async Task AppendLogAsync(
        IRuntSyncLogRepository repo,
        Guid tenantId,
        string provider,
        string outcome,
        string? failoverFrom,
        string plate,
        DateTimeOffset now,
        CancellationToken ct,
        string? extraPayload = null)
    {
        var payload = extraPayload ?? $$"""{"plate":"{{plate}}"}""";
        var entry = RuntSyncLogEntry.Create(
            tenantId,
            provider,
            Operation,
            outcome,
            failoverFrom,
            payload,
            now);
        await repo.AddAsync(entry, ct);
    }
}
