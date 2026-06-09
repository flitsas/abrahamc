namespace Flit.Modules.Companies.Domain;

/// <summary>Bitácora integrations.runt_sync_log (#9447 contingencia).</summary>
public sealed class RuntSyncLogEntry
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string? FailoverFrom { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset SyncedAt { get; private set; }

    private RuntSyncLogEntry() { }

    public static RuntSyncLogEntry Create(
        Guid tenantId,
        string provider,
        string operation,
        string outcome,
        string? failoverFrom,
        string payloadJson,
        DateTimeOffset syncedAt)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido", nameof(tenantId));
        if (!RuntProviderCode.All.Contains(provider))
            throw new ArgumentException($"Provider inválido: {provider}", nameof(provider));

        return new RuntSyncLogEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Provider = provider,
            Operation = operation,
            Outcome = outcome,
            FailoverFrom = failoverFrom,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            SyncedAt = syncedAt,
        };
    }
}
