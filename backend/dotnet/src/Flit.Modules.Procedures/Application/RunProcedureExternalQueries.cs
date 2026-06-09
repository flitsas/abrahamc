using System.Text.Json;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU MTR-04 #9428 — consultas externas concurrentes con fallo aislado (CF-D7).</summary>
public static class RunProcedureExternalQueries
{
    public sealed record Command(
        Guid ProcedureInstanceId,
        Guid TenantId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        string DocumentTypeCode,
        Guid ExecutedByUserId,
        IReadOnlyDictionary<string, string?>? CapturedFields,
        IReadOnlyList<string>? OmittedConnectorCodes = null);

    public sealed record QueryRunItem(
        string ConnectorCode,
        string? EdgeRole,
        string Status,
        bool Succeeded,
        bool CircuitOpen,
        bool Mandatory,
        Guid? IntegrationCallId,
        Guid SnapshotId);

    public sealed record Response(
        Guid ProcedureInstanceId,
        IReadOnlyList<QueryRunItem> Results,
        bool CanContinue);

    public enum RunQueriesErrorKind
    {
        InstanceNotFound,
        InvalidState,
        IncorporationFailed,
    }

    public sealed record RunQueriesError(RunQueriesErrorKind Kind, string Message);

    public static async Task<Result<Response, RunQueriesError>> HandleAsync(
        Command command,
        IProcedureInstanceRepository instanceRepo,
        IProceduresConfigReadRepository configRepo,
        IDocumentTypesReadRepository documentTypesRepo,
        IExternalQueryProvider queryProvider,
        IExternalQueryCallLogRepository callLogRepo,
        IRuntSyncLogRepository runtSyncLogRepo,
        IProcedureQueryResultRepository queryResultRepo,
        ExternalQueryCircuitBreaker circuitBreaker,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(command.ProcedureInstanceId, command.TenantId, ct);
        if (instance is null)
        {
            return Result<Response, RunQueriesError>.Failure(
                new RunQueriesError(RunQueriesErrorKind.InstanceNotFound, "Instancia de trámite no encontrada."));
        }

        if (!string.Equals(instance.State, ProcedureStates.Borrador, StringComparison.OrdinalIgnoreCase))
        {
            return Result<Response, RunQueriesError>.Failure(
                new RunQueriesError(
                    RunQueriesErrorKind.InvalidState,
                    "Las consultas externas solo pueden ejecutarse en estado borrador."));
        }

        var (incorporation, incError) = await IncorporateProcedureActors.HandleAsync(
            new IncorporateProcedureActors.Command(
                command.TenantId,
                command.ProcedureTypeCode,
                command.TrafficAgencyId,
                command.EdgeCode,
                command.DocumentTypeCode,
                command.CapturedFields),
            configRepo,
            documentTypesRepo,
            ct);

        if (incError is not null)
        {
            return Result<Response, RunQueriesError>.Failure(
                new RunQueriesError(RunQueriesErrorKind.IncorporationFailed, incError.Message));
        }

        var omitted = new HashSet<string>(
            command.OmittedConnectorCodes ?? [],
            StringComparer.OrdinalIgnoreCase);

        var plan = incorporation!.QueriesToExecute
            .Where(q => !omitted.Contains(q.ConnectorCode))
            .ToList();

        var payload = BuildQueryPayload(command.CapturedFields, command.DocumentTypeCode);
        var now = DateTimeOffset.UtcNow;

        var tasks = plan.Select(q => ExecuteOneAsync(
            command,
            instance,
            q,
            payload,
            now,
            queryProvider,
            callLogRepo,
            runtSyncLogRepo,
            queryResultRepo,
            circuitBreaker,
            ct));

        var items = await Task.WhenAll(tasks);

        // CF-D7: fallo aislado — una consulta fallida no tumba el lote ni la instancia.
        _ = items;

        return Result<Response, RunQueriesError>.Success(
            new Response(command.ProcedureInstanceId, items, CanContinue: true));
    }

    private static async Task<QueryRunItem> ExecuteOneAsync(
        Command command,
        ProcedureInstance instance,
        QueryExecutionPlan plan,
        JsonElement payload,
        DateTimeOffset requestedAt,
        IExternalQueryProvider queryProvider,
        IExternalQueryCallLogRepository callLogRepo,
        IRuntSyncLogRepository runtSyncLogRepo,
        IProcedureQueryResultRepository queryResultRepo,
        ExternalQueryCircuitBreaker circuitBreaker,
        CancellationToken ct)
    {
        var idempotencyKey = $"{command.ProcedureInstanceId:N}:{plan.ConnectorCode}:{plan.EdgeCode ?? ""}";

        var execResult = await ExecuteExternalQuery.HandleAsync(
            new ExecuteExternalQuery.Command(
                command.TenantId,
                plan.ConnectorCode,
                idempotencyKey,
                payload,
                command.ProcedureInstanceId,
                plan.EdgeCode),
            queryProvider,
            callLogRepo,
            runtSyncLogRepo,
            circuitBreaker,
            ct);

        var succeeded = execResult.IsSuccess && execResult.Value!.Succeeded && !execResult.Value.CircuitOpen;
        var status = execResult.IsSuccess switch
        {
            true when execResult.Value!.CircuitOpen => "failed",
            true when execResult.Value!.Succeeded => "ok",
            _ => "failed",
        };

        var resultBody = execResult.IsSuccess
            ? execResult.Value!.ResponseBody ?? JsonSerializer.SerializeToElement(new { status })
            : JsonSerializer.SerializeToElement(new { error = execResult.Error!.Message });

        var snapshotId = Guid.CreateVersion7();
        var respondedAt = DateTimeOffset.UtcNow;

        await queryResultRepo.UpsertAsync(
            new ProcedureQueryResultRecord(
                snapshotId,
                command.TenantId,
                command.ProcedureInstanceId,
                plan.ConnectorCode,
                plan.EdgeCode,
                Source: plan.ConnectorCode,
                status,
                resultBody,
                requestedAt,
                respondedAt,
                execResult.IsSuccess ? execResult.Value!.CallId : null,
                command.ExecutedByUserId,
                command.ExecutedByUserId),
            ct);

        return new QueryRunItem(
            plan.ConnectorCode,
            plan.EdgeCode,
            status,
            succeeded,
            execResult.IsSuccess && execResult.Value!.CircuitOpen,
            plan.IsMandatory,
            execResult.IsSuccess ? execResult.Value!.CallId : null,
            snapshotId);
    }

    private static JsonElement BuildQueryPayload(
        IReadOnlyDictionary<string, string?>? capturedFields,
        string documentTypeCode)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (capturedFields is not null)
        {
            foreach (var (key, value) in capturedFields)
            {
                dict[key] = value;
            }
        }

        var docType = dict.GetValueOrDefault("tipo_documento")?.ToString()
            ?? dict.GetValueOrDefault("documentType")?.ToString()
            ?? documentTypeCode;
        dict["documentType"] = docType;
        dict["documentTypeCode"] = docType;

        var docNumber = dict.GetValueOrDefault("documentNumber")?.ToString()
            ?? dict.GetValueOrDefault("numero_documento")?.ToString()
            ?? dict.GetValueOrDefault("doc_propietario")?.ToString()
            ?? dict.GetValueOrDefault("doc_comprador")?.ToString()
            ?? dict.GetValueOrDefault("doc_locatario")?.ToString();

        if (!string.IsNullOrWhiteSpace(docNumber))
        {
            dict["documentNumber"] = docNumber;
        }

        var plate = dict.GetValueOrDefault("placa")?.ToString()
            ?? dict.GetValueOrDefault("plate")?.ToString();
        if (!string.IsNullOrWhiteSpace(plate))
        {
            dict["plate"] = plate;
        }

        return JsonSerializer.SerializeToElement(dict);
    }
}
