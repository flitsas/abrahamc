using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Integrations.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlRuntSyncLogRepository(FlitDbContext db) : IRuntSyncLogRepository
{
    public async Task LogAsync(RuntSyncLogEntry entry, CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO integrations.runt_sync_log (
              id, tenant_id, provider, operation, outcome, failover_from, payload, synced_at
            ) VALUES (
              @id, @tenant_id, @provider, @operation, @outcome, @failover_from, @payload::jsonb, @synced_at
            )
            """;

        Add(cmd, "id", entry.Id);
        Add(cmd, "tenant_id", entry.TenantId);
        Add(cmd, "provider", entry.Provider);
        Add(cmd, "operation", entry.Operation);
        Add(cmd, "outcome", entry.Outcome);
        Add(cmd, "failover_from", (object?)entry.FailoverFrom ?? DBNull.Value);
        Add(cmd, "payload", entry.PayloadJson);
        Add(cmd, "synced_at", entry.SyncedAt.UtcDateTime);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (IsMissingRuntSyncLog(ex))
        {
        }
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsMissingRuntSyncLog(Exception ex) =>
        ex.Message.Contains("runt_sync_log", StringComparison.OrdinalIgnoreCase);
}
