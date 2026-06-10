using System.Data.Common;
using Npgsql;

namespace Flit.Infrastructure.MultiTenant;

/// <summary>Aplica GUCs de sesión en conexiones Npgsql abiertas por EF Core.</summary>
public static class TenantGucApplicator
{
    public static async Task ApplyAsync(
        DbConnection connection,
        ITenantContext context,
        CancellationToken cancellationToken = default)
    {
        if (connection is not NpgsqlConnection npgsql || !context.IsConfigured)
            return;

        if (npgsql.State != System.Data.ConnectionState.Open)
            await npgsql.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = npgsql.CreateCommand();
        cmd.CommandText = BuildSetConfigBatch(context);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string BuildSetConfigBatch(ITenantContext context)
    {
        var parts = new List<string>();

        if (context.TenantId.HasValue)
            parts.Add(Set(TenantGucNames.CurrentTenantId, context.TenantId.Value.ToString()));

        if (context.UserId.HasValue)
            parts.Add(Set(TenantGucNames.CurrentUserId, context.UserId.Value.ToString()));

        parts.Add(Set(TenantGucNames.IsSuperAdmin, context.IsSuperAdmin ? "true" : "false"));

        if (context.TrafficAgencyId.HasValue)
            parts.Add(Set(TenantGucNames.CurrentAgencyId, context.TrafficAgencyId.Value.ToString()));

        if (context.RequestId.HasValue)
            parts.Add(Set(TenantGucNames.RequestId, context.RequestId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(context.ClientIp))
            parts.Add(Set(TenantGucNames.ClientIp, context.ClientIp));

        return string.Join("; ", parts);
    }

    private static string Set(string name, string value) =>
        $"SELECT set_config('{name}', '{Escape(value)}', false)";
    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
