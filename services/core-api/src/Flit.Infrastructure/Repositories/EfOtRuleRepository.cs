using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF Core para ot.ot_rules (HU #9456 OT-03).
/// La tabla usa RLS por app.current_agency_id (ADR-0012).
/// </summary>
public sealed class EfOtRuleRepository(FlitDbContext db) : IOtRuleRepository
{
    public async Task<OtRule?> GetByIdAsync(
        Guid id,
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        return await db.OtRules
            .FirstOrDefaultAsync(
                x => x.Id == id && x.TrafficAgencyId == trafficAgencyId && x.DeletedAt == null,
                ct);
    }

    public async Task<IReadOnlyList<OtRule>> ListByAgencyAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        return await db.OtRules
            .AsNoTracking()
            .Where(x => x.TrafficAgencyId == trafficAgencyId && x.DeletedAt == null)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OtRule>> ListActiveByTriggerAsync(
        Guid trafficAgencyId,
        string triggerEvent,
        CancellationToken ct = default)
    {
        return await db.OtRules
            .AsNoTracking()
            .Where(x =>
                x.TrafficAgencyId == trafficAgencyId &&
                x.DeletedAt == null &&
                x.IsActive &&
                x.TriggerEvent == triggerEvent)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(OtRule rule, CancellationToken ct = default)
    {
        await db.OtRules.AddAsync(rule, ct);
    }

    public Task UpdateAsync(OtRule rule, CancellationToken ct = default)
    {
        db.OtRules.Update(rule);
        return Task.CompletedTask;
    }
}
