namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Evento de webhook inbound/outbound QX — integrations.webhook_events (HU #9459 INT-02).
/// Tabla append-only con idempotencia garantizada por UNIQUE (idempotency_key).
/// </summary>
public sealed class WebhookEvent
{
    public static class Directions
    {
        public const string Inbound = "inbound";
        public const string Outbound = "outbound";
    }

    public static class Statuses
    {
        public const string Received = "received";
        public const string Processed = "processed";
        public const string Failed = "failed";
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? TrafficAgencyId { get; private set; }
    public string Direction { get; private set; } = Directions.Inbound;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public string? Signature { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Status { get; private set; } = Statuses.Received;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private WebhookEvent() { }

    /// <summary>Crea un evento inbound desde un POST de QX.</summary>
    public static WebhookEvent CreateInbound(
        Guid tenantId,
        Guid? trafficAgencyId,
        string eventType,
        string payloadJson,
        string idempotencyKey,
        string? signature,
        DateTimeOffset receivedAt)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType requerido", nameof(eventType));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey requerido", nameof(idempotencyKey));

        return new WebhookEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            TrafficAgencyId = trafficAgencyId,
            Direction = Directions.Inbound,
            EventType = eventType.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            IdempotencyKey = idempotencyKey.Trim(),
            Signature = signature,
            Status = Statuses.Received,
            ReceivedAt = receivedAt,
            ProcessedAt = null,
        };
    }

    /// <summary>Marca el evento como procesado (AC2: persiste processed_at).</summary>
    public void MarkProcessed(DateTimeOffset processedAt)
    {
        Status = Statuses.Processed;
        ProcessedAt = processedAt;
    }

    /// <summary>Marca el evento como fallido.</summary>
    public void MarkFailed()
    {
        Status = Statuses.Failed;
    }
}
