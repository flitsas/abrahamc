using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfCompanyModuleConfigsRepository(FlitDbContext db) : ICompanyModuleConfigsRepository
{
    public async Task<IReadOnlyList<CompanyModuleConfig>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var list = await db.CompanyModuleConfigs
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.ModuleKey)
            .ToListAsync(ct);
        return list;
    }

    public Task<CompanyModuleConfig?> GetByTenantAndModuleAsync(
        Guid tenantId,
        string moduleKey,
        CancellationToken ct = default)
        => db.CompanyModuleConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ModuleKey == moduleKey, ct);

    public async Task AddAsync(CompanyModuleConfig config, CancellationToken ct = default)
        => await db.CompanyModuleConfigs.AddAsync(config, ct);

    public void Update(CompanyModuleConfig config) => db.CompanyModuleConfigs.Update(config);

    public Task<SignatureWallet?> GetSignatureWalletByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => db.SignatureWallets.FirstOrDefaultAsync(w => w.TenantId == tenantId, ct);
}
