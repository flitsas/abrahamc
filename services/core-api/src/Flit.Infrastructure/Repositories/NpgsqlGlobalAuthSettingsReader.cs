using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlGlobalAuthSettingsReader(FlitDbContext db) : IGlobalAuthSettingsReader
{
    public async Task<GlobalAuthSettingsRow> GetAsync(CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT invitation_ttl_minutes,
                   password_reset_ttl_minutes,
                   access_token_ttl_minutes,
                   refresh_token_ttl_days
            FROM identity.global_auth_settings
            LIMIT 1
            """,
            conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return GlobalAuthSettingsRow.Default;

        return new GlobalAuthSettingsRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }
}
