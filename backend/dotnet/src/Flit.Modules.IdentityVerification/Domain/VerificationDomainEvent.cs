namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Bitacora append-only para idempotencia de TRAMITE_CREATED.
/// Tabla: identity_verification.verification_domain_events.
/// </summary>
public sealed class VerificationDomainEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid ProcedureInstanceId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }

    private VerificationDomainEvent() { }

    public static VerificationDomainEvent CreateTramiteCreated(
        Guid tenantId,
        Guid procedureInstanceId,
        string idempotencyKey,
        string payloadJson,
        DateTimeOffset processedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new VerificationDomainEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            EventType = VerificationEventTypes.TramiteCreated,
            ProcedureInstanceId = procedureInstanceId,
            PayloadJson = payloadJson,
            IdempotencyKey = idempotencyKey,
            ProcessedAt = processedAt,
        };
    }
}

public static class VerificationEventTypes
{
    public const string TramiteCreated = "TRAMITE_CREATED";
}
