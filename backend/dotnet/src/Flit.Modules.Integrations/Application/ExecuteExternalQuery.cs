using System.Diagnostics;
using System.Text.Json;
using Flit.Modules.Integrations.Domain;
using Flit.Modules.Integrations.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Integrations.Application;

/// <summary>Orquesta consulta externa con bitácora, circuit breaker y reintentos (INT-01 #9431).</summary>
public static class ExecuteExternalQuery
{
    public const int MaxTransientRetries = 2;

    public sealed record Command(
        Guid TenantId,
        string QueryConnectorCode,
        string IdempotencyKey,
        JsonElement Payload,
        Guid? ProcedureInstanceId = null,
        string? EdgeRole = null);

    public sealed record Response(
        Guid CallId,
        bool Succeeded,
        bool FromCache,
        bool CircuitOpen,
        int? HttpStatus,
        JsonElement? ResponseBody);

    public static async Task<Result<Response, ExternalQueryError>> HandleAsync(
        Command command,
        IExternalQueryProvider provider,
        IExternalQueryCallLogRepository callLogRepo,
        IRuntSyncLogRepository runtSyncLogRepo,
        ExternalQueryCircuitBreaker circuitBreaker,
        CancellationToken ct = default)
    {
        var connector = command.QueryConnectorCode?.Trim() ?? string.Empty;
        var idempotencyKey = command.IdempotencyKey?.Trim() ?? string.Empty;

        if (command.TenantId == Guid.Empty)
        {
            return Result<Response, ExternalQueryError>.Failure(
                new ExternalQueryError(ExternalQueryErrorKind.Validation, "tenantId es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(connector))
        {
            return Result<Response, ExternalQueryError>.Failure(
                new ExternalQueryError(ExternalQueryErrorKind.Validation, "queryConnectorCode es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result<Response, ExternalQueryError>.Failure(
                new ExternalQueryError(ExternalQueryErrorKind.Validation, "idempotencyKey es obligatorio."));
        }

        var cached = await callLogRepo.FindCompletedByIdempotencyKeyAsync(
            command.TenantId, connector, idempotencyKey, ct);

        if (cached is not null)
        {
            return Result<Response, ExternalQueryError>.Success(
                new Response(
                    cached.Id,
                    cached.Succeeded,
                    FromCache: true,
                    CircuitOpen: false,
                    cached.HttpStatus,
                    cached.Response));
        }

        if (circuitBreaker.IsOpen(command.TenantId, connector))
        {
            var circuitCallId = await LogCallAsync(
                callLogRepo,
                command,
                connector,
                idempotencyKey,
                succeeded: false,
                httpStatus: null,
                response: null,
                latencyMs: 0,
                errorMessage: "circuit_open",
                ct);

            await MaybeLogRuntCircuitAsync(runtSyncLogRepo, command, connector, ct);

            return Result<Response, ExternalQueryError>.Success(
                new Response(circuitCallId, false, FromCache: false, CircuitOpen: true, null, null));
        }

        var wrappedRequest = ExternalQueryPayloadMetadata.Wrap(command.Payload, idempotencyKey);
        var providerRequest = new ExternalQueryProviderRequest(
            command.TenantId,
            connector,
            command.ProcedureInstanceId,
            command.EdgeRole,
            ExternalQueryPayloadMetadata.UnwrapBusinessPayload(wrappedRequest));

        ExternalQueryProviderResult? lastResult = null;
        var attempts = 0;

        while (attempts <= MaxTransientRetries)
        {
            attempts++;
            try
            {
                lastResult = await provider.ExecuteAsync(providerRequest, ct);
                break;
            }
            catch (TransientExternalQueryException) when (attempts <= MaxTransientRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempts), ct);
            }
        }

        if (lastResult is null)
        {
            circuitBreaker.RecordFailure(command.TenantId, connector);
            var failId = await LogCallAsync(
                callLogRepo,
                command,
                connector,
                idempotencyKey,
                succeeded: false,
                httpStatus: null,
                response: null,
                latencyMs: 0,
                errorMessage: "transient_exhausted",
                ct);

            return Result<Response, ExternalQueryError>.Failure(
                new ExternalQueryError(
                    ExternalQueryErrorKind.ProviderFailed,
                    "La consulta externa falló tras reintentos transitorios."));
        }

        if (lastResult.Succeeded)
        {
            circuitBreaker.RecordSuccess(command.TenantId, connector);
        }
        else
        {
            circuitBreaker.RecordFailure(command.TenantId, connector);
        }

        var callId = await LogCallAsync(
            callLogRepo,
            command,
            connector,
            idempotencyKey,
            lastResult.Succeeded,
            lastResult.HttpStatus,
            lastResult.Response,
            lastResult.LatencyMs,
            lastResult.ErrorMessage,
            ct);

        return Result<Response, ExternalQueryError>.Success(
            new Response(
                callId,
                lastResult.Succeeded,
                FromCache: false,
                CircuitOpen: false,
                lastResult.HttpStatus,
                lastResult.Response));
    }

    private static async Task<Guid> LogCallAsync(
        IExternalQueryCallLogRepository callLogRepo,
        Command command,
        string connector,
        string idempotencyKey,
        bool succeeded,
        int? httpStatus,
        JsonElement? response,
        int latencyMs,
        string? errorMessage,
        CancellationToken ct)
    {
        var wrapped = ExternalQueryPayloadMetadata.Wrap(command.Payload, idempotencyKey);
        var sanitizedRequest = ExternalQueryLogSanitizer.Sanitize(wrapped);
        var sanitizedResponse = response is { } r ? ExternalQueryLogSanitizer.Sanitize(r) : (JsonElement?)null;

        var callId = Guid.CreateVersion7();
        await callLogRepo.LogAsync(
            new ExternalQueryCallLogEntry(
                callId,
                command.TenantId,
                command.ProcedureInstanceId,
                connector,
                command.EdgeRole,
                sanitizedRequest,
                sanitizedResponse,
                httpStatus,
                latencyMs,
                succeeded,
                errorMessage,
                DateTimeOffset.UtcNow),
            ct);

        return callId;
    }

    private static async Task MaybeLogRuntCircuitAsync(
        IRuntSyncLogRepository runtSyncLogRepo,
        Command command,
        string connector,
        CancellationToken ct)
    {
        if (!connector.Contains("RUNT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await runtSyncLogRepo.LogAsync(
            new RuntSyncLogEntry(
                Guid.CreateVersion7(),
                command.TenantId,
                Provider: "runt",
                Operation: connector,
                Outcome: "circuit_open",
                FailoverFrom: null,
                PayloadJson: """{"reason":"circuit_open"}""",
                DateTimeOffset.UtcNow),
            ct);
    }
}
