using System.Text.Json;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// INT-01 #9431 — consultas externas (mock DEV).
/// INT-02 #9459 — webhooks QX inbound con idempotencia.
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

        group.MapPost("/webhooks/qx", async (
            QxWebhookRequest req,
            IWebhookEventRepository repo,
            FlitDbContext db,
            IClock clock,
            HttpContext ctx,
            CancellationToken ct) =>
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

            var cmd = new ReceiveQxWebhook.Command(
                TenantId: tenantId,
                TrafficAgencyId: req.TrafficAgencyId,
                EventType: req.EventType,
                PayloadJson: req.PayloadJson,
                IdempotencyKey: req.IdempotencyKey,
                Signature: req.Signature);

            var result = await ReceiveQxWebhook.HandleAsync(
                cmd,
                repo,
                async cancellationToken => await db.SaveChangesAsync(cancellationToken),
                clock,
                ct);

            return Results.Ok(new
            {
                event_id = result.EventId == Guid.Empty ? (Guid?)null : result.EventId,
                direction = result.Direction,
                payload = result.PayloadJson,
                processed_at = result.ProcessedAt,
                idempotent = result.WasIdempotent,
            });
        })
        .WithName("ReceiveQxWebhook")
        .WithSummary("Recibe un webhook QX inbound con idempotencia (INT-02)");
    }
}
