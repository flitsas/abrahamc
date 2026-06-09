using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

public interface ICompanyModuleConfigsRepository
{
    Task<IReadOnlyList<CompanyModuleConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<CompanyModuleConfig?> GetByTenantAndModuleAsync(
        Guid tenantId,
        string moduleKey,
        CancellationToken ct = default);
    Task AddAsync(CompanyModuleConfig config, CancellationToken ct = default);
    void Update(CompanyModuleConfig config);
    Task<SignatureWallet?> GetSignatureWalletByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
