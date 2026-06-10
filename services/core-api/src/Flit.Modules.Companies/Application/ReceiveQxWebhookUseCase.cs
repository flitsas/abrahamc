using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9459 INT-02 — Webhooks QX inbound con idempotencia.
/// AC1: idempotency_key repetida → 200 sin reejecutar efectos.
/// AC2: evento nuevo → persiste payload, direction, processed_at en integrations.webhook_events.
/// </summary>
public static class ReceiveQxWebhook
{
    public sealed record Command(
        Guid TenantId,
        Guid? TrafficAgencyId,
        string EventType,
        string PayloadJson,
        string IdempotencyKey,
        string? Signature);

    public sealed record Response(
        Guid EventId,
        string Direction,
        string PayloadJson,
        DateTimeOffset? ProcessedAt,
        bool WasIdempotent);

    public static async Task<Response> HandleAsync(
        Command cmd,
        IWebhookEventRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        // AC1: verificar idempotencia — clave repetida → 200 sin efectos secundarios
        var alreadyExists = await repo.ExistsByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (alreadyExists)
        {
            return new Response(
                EventId: Guid.Empty,
                Direction: WebhookEvent.Directions.Inbound,
                PayloadJson: cmd.PayloadJson,
                ProcessedAt: null,
                WasIdempotent: true);
        }

        // AC2: persiste evento con payload, direction y processed_at
        var now = clock.UtcNow;
        var entry = WebhookEvent.CreateInbound(
            cmd.TenantId,
            cmd.TrafficAgencyId,
            cmd.EventType,
            cmd.PayloadJson,
            cmd.IdempotencyKey,
            cmd.Signature,
            now);

        entry.MarkProcessed(now);

        await repo.AddAsync(entry, ct);
        await saveChanges(ct);

        return new Response(
            EventId: entry.Id,
            Direction: entry.Direction,
            PayloadJson: entry.PayloadJson,
            ProcessedAt: entry.ProcessedAt,
            WasIdempotent: false);
    }
}
