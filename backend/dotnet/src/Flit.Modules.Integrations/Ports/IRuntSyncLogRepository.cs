namespace Flit.Modules.Integrations.Ports;

public sealed record RuntSyncLogEntry(
    Guid Id,
    Guid TenantId,
    string Provider,
    string Operation,
    string Outcome,
    string? FailoverFrom,
    string PayloadJson,
    DateTimeOffset SyncedAt);

/// <summary>Trazas RUNT/Verifik — <c>integrations.runt_sync_log</c> (INT-01 #9431).</summary>
public interface IRuntSyncLogRepository
{
    Task LogAsync(RuntSyncLogEntry entry, CancellationToken ct = default);
}
