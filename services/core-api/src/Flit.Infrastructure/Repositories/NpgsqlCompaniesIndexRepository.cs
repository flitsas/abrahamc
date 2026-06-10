using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlCompaniesIndexRepository(FlitDbContext db) : ICompaniesIndexRepository
{
    public async Task<(IReadOnlyList<CompanyIndexRow> Items, int Total)> ListAsync(
        CompanyIndexListQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var limit = Math.Clamp(query.Limit, 1, 200);
        var offset = (page - 1) * limit;
        var search = NormalizeSearch(query.Nit, query.Name);
        var useTrgm = search is { Length: >= 3 };

        var (conn, tenantFilter) = await OpenWithTenantSessionAsync(ct);

        await using var countCmd = BuildListCommand(conn, search, useTrgm, query, tenantFilter, countOnly: true);
        var total = Convert.ToInt32(
            await countCmd.ExecuteScalarAsync(ct) ?? 0,
            System.Globalization.CultureInfo.InvariantCulture);

        await using var listCmd = BuildListCommand(conn, search, useTrgm, query, tenantFilter, countOnly: false);
        listCmd.Parameters.AddWithValue("limit", limit);
        listCmd.Parameters.AddWithValue("offset", offset);

        var items = new List<CompanyIndexRow>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapRow(reader));
        }

        return (items, total);
    }

    public async Task<CompanyIndexRow?> GetByIdAsync(Guid companyId, CancellationToken ct = default)
    {
        var (conn, tenantFilter) = await OpenWithTenantSessionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.id, c.tenant_id, t.name, c.nit, c.legal_name, c.commercial_name,
                   c.modules_enabled::text, c.created_at, c.updated_at
            FROM companies.companies c
            INNER JOIN identity.tenants t ON t.id = c.tenant_id AND t.deleted_at IS NULL
            WHERE c.deleted_at IS NULL
              AND c.id = @id
              AND (@tenant_id::uuid IS NULL OR c.tenant_id = @tenant_id::uuid)
            """;
        cmd.Parameters.AddWithValue("id", companyId);
        cmd.Parameters.AddWithValue("tenant_id", (object?)tenantFilter ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        db.Companies.Update(company);
        return Task.CompletedTask;
    }

    private async Task<(NpgsqlConnection Conn, Guid? TenantFilter)> OpenWithTenantSessionAsync(
        CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        var session = CompaniesSessionAmbient.Get();
        await TenantSessionGuc.ApplyAsync(conn, session, ct);

        Guid? tenantFilter = session is { IsSuperAdmin: false, TenantId: var tenantId }
            ? tenantId
            : null;

        return (conn, tenantFilter);
    }

    private static NpgsqlCommand BuildListCommand(
        NpgsqlConnection conn,
        string? search,
        bool useTrgm,
        CompanyIndexListQuery query,
        Guid? tenantFilter,
        bool countOnly)
    {
        var cmd = conn.CreateCommand();
        var select = countOnly
            ? "SELECT COUNT(*)::int"
            : """
              SELECT c.id, c.tenant_id, t.name, c.nit, c.legal_name, c.commercial_name,
                     c.modules_enabled::text, c.created_at, c.updated_at
              """;

        cmd.CommandText = $"""
            {select}
            FROM companies.companies c
            INNER JOIN identity.tenants t ON t.id = c.tenant_id AND t.deleted_at IS NULL
            WHERE c.deleted_at IS NULL
              AND (@tenant_id::uuid IS NULL OR c.tenant_id = @tenant_id::uuid)
              AND (@company_id::uuid IS NULL OR c.id = @company_id::uuid)
              AND (@created_from::timestamptz IS NULL OR c.created_at >= @created_from::timestamptz)
              AND (@created_to::timestamptz IS NULL OR c.created_at <= @created_to::timestamptz)
              AND (
                @search::text IS NULL
                OR c.nit ILIKE '%' || @search::text || '%'
                OR c.legal_name ILIKE '%' || @search::text || '%'
                OR COALESCE(c.commercial_name, '') ILIKE '%' || @search::text || '%'
                OR (@use_trgm AND (c.nit % @search::text OR c.legal_name % @search::text))
              )
            """;

        if (!countOnly)
        {
            cmd.CommandText += " ORDER BY c.created_at DESC LIMIT @limit OFFSET @offset";
        }

        cmd.Parameters.AddWithValue("tenant_id", (object?)tenantFilter ?? DBNull.Value);
        cmd.Parameters.AddWithValue("company_id", (object?)query.CompanyId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_from", (object?)query.CreatedFrom ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_to", (object?)query.CreatedTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("use_trgm", useTrgm);
        return cmd;
    }

    private static string? NormalizeSearch(string? nit, string? name)
    {
        var nitTerm = string.IsNullOrWhiteSpace(nit) ? null : nit.Trim();
        var nameTerm = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (nitTerm is not null && nameTerm is not null)
        {
            return $"{nitTerm} {nameTerm}";
        }

        return nitTerm ?? nameTerm;
    }

    private static CompanyIndexRow MapRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6),
        reader.GetFieldValue<DateTimeOffset>(7),
        reader.GetFieldValue<DateTimeOffset>(8));
}
