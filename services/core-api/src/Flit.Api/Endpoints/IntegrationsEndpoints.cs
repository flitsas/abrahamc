using System.Text.Json;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// INT-01 #9431 — consultas externas (mock DEV).
/// INT-02 #9459 / HU #9698 — webhooks QX inbound con idempotencia y logs.
/// </summary>
public static class IntegrationsEndpoints
{
    public sealed record ExecuteExternalQueryRequest(
        Guid TenantId,
        string QueryConnectorCode,
        string IdempotencyKey,
        JsonElement? Payload,
        Guid? ProcedureInstanceId,
        string? EdgeRole);

    public sealed record QxWebhookRequest(
        string EventType,
        string PayloadJson,
        string IdempotencyKey,
        Guid? TrafficAgencyId = null,
        string? Signature = null);

    public static void MapIntegrationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integrations")
            .WithTags("Integrations");

        group.MapPost("/external-queries/execute", async (
            ExecuteExternalQueryRequest req,
            IExternalQueryProvider provider,
            IExternalQueryCallLogRepository callLogRepo,
            Flit.Modules.Integrations.Ports.IRuntSyncLogRepository runtSyncLogRepo,
            ExternalQueryCircuitBreaker circuitBreaker,
            CancellationToken ct) =>
        {
            var payload = req.Payload ?? JsonDocument.Parse("{}").RootElement;

            var result = await ExecuteExternalQuery.HandleAsync(
                new ExecuteExternalQuery.Command(
                    req.TenantId,
                    req.QueryConnectorCode,
                    req.IdempotencyKey,
                    payload,
                    req.ProcedureInstanceId,
                    req.EdgeRole),
                provider,
                callLogRepo,
                runtSyncLogRepo,
                circuitBreaker,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    callId = ok.CallId,
                    succeeded = ok.Succeeded,
                    fromCache = ok.FromCache,
                    circuitOpen = ok.CircuitOpen,
                    httpStatus = ok.HttpStatus,
                    response = ok.ResponseBody,
                }),
                err => err.Kind switch
                {
                    ExternalQueryErrorKind.Validation => Results.BadRequest(new { error = err.Message }),
                    _ => Results.Json(new { error = err.Message }, statusCode: StatusCodes.Status502BadGateway),
                });
        })
        .WithName("ExecuteExternalQuery")
        .WithSummary("Ejecuta consulta externa con bitácora e idempotencia (INT-01)");

        group.MapPost("/webhooks/qx", HandleQxWebhook)
        .WithName("ReceiveQxWebhook")
        .WithSummary("Recibe un webhook QX inbound con idempotencia (INT-02)");

        group.MapPost("/quipux/webhook", HandleQxWebhook)
        .WithName("ReceiveQuipuxWebhook")
        .WithSummary("Alias Quipux mock webhook (HU #9698)");

        group.MapGet("/logs", async (
            Guid trafficAgencyId,
            IIntegrationLogRepository logRepo,
            HttpContext ctx,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!ctx.Request.Headers.TryGetValue("X-Flit-Tenant-Id", out var tenantHeader)
                || !Guid.TryParse(tenantHeader, out var tenantId))
            {
                return Results.BadRequest(new { error = "X-Flit-Tenant-Id requerido." });
            }

            var (items, total) = await logRepo.ListByAgencyAsync(
                tenantId, trafficAgencyId, page, pageSize, ct);

            return Results.Ok(new
            {
                total,
                page,
                pageSize,
                items = items.Select(i => new
                {
                    id = i.Id,
                    provider = i.Provider,
                    direction = i.Direction,
                    event_type = i.EventType,
                    payload = i.PayloadJson,
                    http_status = i.HttpStatus,
                    result = i.Result,
                    latency_ms = i.LatencyMs,
                    called_at = i.CalledAt,
                }),
            });
        })
        .WithName("ListIntegrationLogs")
        .WithSummary("Lista logs de integración por OT (HU #9698)");
    }

    private static async Task<IResult> HandleQxWebhook(
        QxWebhookRequest req,
        IQuipuxWebhookAdapter adapter,
        IWebhookEventRepository webhookRepo,
        IIntegrationLogRepository logRepo,
        IProcedureInstanceRepository procedureRepo,
        IProcedureStateHistoryRepository historyRepo,
        FlitDbContext db,
        IClock clock,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.EventType))
            return Results.BadRequest(new { error = "event_type es requerido." });

        if (string.IsNullOrWhiteSpace(req.IdempotencyKey))
            return Results.BadRequest(new { error = "idempotency_key es requerido." });

        if (!ctx.Request.Headers.TryGetValue("X-Flit-Tenant-Id", out var tenantHeader)
            || !Guid.TryParse(tenantHeader, out var tenantId))
        {
            return Results.BadRequest(new { error = "X-Flit-Tenant-Id requerido." });
        }

        var signature = req.Signature;
        if (string.IsNullOrWhiteSpace(signature) &&
            ctx.Request.Headers.TryGetValue("X-Quipux-Signature", out var headerSig))
        {
            signature = headerSig.ToString();
        }

        var cmd = new ReceiveQxWebhook.Command(
            TenantId: tenantId,
            TrafficAgencyId: req.TrafficAgencyId,
            EventType: req.EventType,
            PayloadJson: req.PayloadJson,
            IdempotencyKey: req.IdempotencyKey,
            Signature: signature);

        var result = await ReceiveQxWebhook.HandleAsync(
            cmd,
            adapter,
            webhookRepo,
            logRepo,
            procedureRepo,
            historyRepo,
            async cancellationToken => await db.SaveChangesAsync(cancellationToken),
            clock,
            ct);

        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                ReceiveQxWebhook.ErrorCode.InvalidSignature =>
                    Results.Json(new { error = result.Error.Message }, statusCode: StatusCodes.Status401Unauthorized),
                _ => Results.BadRequest(new { error = result.Error.Message }),
            };
        }

        var r = result.Value;
        return Results.Ok(new
        {
            event_id = r.EventId,
            direction = r.Direction,
            payload = r.PayloadJson,
            processed_at = r.ProcessedAt,
            idempotent = r.WasIdempotent,
            procedure_updated = r.ProcedureUpdated,
            procedure_id = r.ProcedureId,
            new_state = r.NewState,
        });
    }
}
