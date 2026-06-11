using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfCompaniesRepository : ICompaniesRepository
{
    private readonly FlitDbContext _db;

    public EfCompaniesRepository(FlitDbContext db) => _db = db;

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Company?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Companies.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

    public Task<bool> ExistsForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Companies.AnyAsync(c => c.TenantId == tenantId, ct);

    public Task<bool> ExistsByNitAsync(string nit, CancellationToken ct = default)
        => _db.Companies.AnyAsync(
            c => c.Nit == nit.Trim() && c.DeletedAt == null,
            ct);

    public async Task AddAsync(Company company, CancellationToken ct = default)
        => await _db.Companies.AddAsync(company, ct);

    public async Task AddModuleConfigAsync(CompanyModuleConfig config, CancellationToken ct = default)
        => await _db.CompanyModuleConfigs.AddAsync(config, ct);

    public async Task AddSignatureWalletAsync(SignatureWallet wallet, CancellationToken ct = default)
        => await _db.SignatureWallets.AddAsync(wallet, ct);

    public async Task AddWalletMovementAsync(SignatureWalletMovement movement, CancellationToken ct = default)
        => await _db.SignatureWalletMovements.AddAsync(movement, ct);

    public async Task AddVehicleOwnershipRuleAsync(VehicleOwnershipRule rule, CancellationToken ct = default)
        => await _db.VehicleOwnershipRules.AddAsync(rule, ct);
}
