using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVehicleOwnershipRulesRepository(FlitDbContext db) : IVehicleOwnershipRulesRepository
{
    public async Task<IReadOnlyList<VehicleOwnershipRule>> ListActiveByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => await db.VehicleOwnershipRules
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

    public Task<VehicleOwnershipRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.VehicleOwnershipRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(VehicleOwnershipRule rule, CancellationToken ct = default)
        => await db.VehicleOwnershipRules.AddAsync(rule, ct);

    public void Update(VehicleOwnershipRule rule) => db.VehicleOwnershipRules.Update(rule);
}
