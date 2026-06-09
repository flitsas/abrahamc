using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Persistence.SeedData;

/// <summary>
/// Aplica seeds del shell vía EF (sin migraciones).
/// </summary>
public static class FlitShellSeedData
{
    public static Task ApplyAllAsync(FlitDbContext db, CancellationToken ct = default) =>
        FlitV2Seeds.ApplyAllAsync(
            sql => db.Database.ExecuteSqlRawAsync(sql, ct),
            ct);
}
