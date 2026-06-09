using Flit.Infrastructure.MultiTenant;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

/// <summary>Aplica GUC de sesión en la conexión EF abierta (HU #9418/#9419).</summary>
internal static class IdentitySessionGucApplicator
{
    public static async Task ApplyAsync(
        FlitDbContext db,
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var context = new FixedTenantContext(tenantId, userId, isSuperAdmin);
        await using var cmd = new NpgsqlCommand(TenantGucApplicator.BuildSetConfigBatch(context), conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed class FixedTenantContext(Guid tenantId, Guid userId, bool isSuperAdmin) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public bool IsSuperAdmin => isSuperAdmin;
        public Guid? TrafficAgencyId => null;
        public Guid? RequestId => null;
        public string? ClientIp => null;
        public bool IsConfigured => true;

        public void Apply(
            Guid? tenantId,
            Guid? userId,
            bool isSuperAdmin,
            Guid? trafficAgencyId = null,
            Guid? requestId = null,
            string? clientIp = null)
        { }
    }
}
