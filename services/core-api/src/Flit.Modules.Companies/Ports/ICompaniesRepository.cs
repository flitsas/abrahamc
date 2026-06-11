using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

public interface ICompaniesRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Company?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsByNitAsync(string nit, CancellationToken ct = default);
    Task AddAsync(Company company, CancellationToken ct = default);
    Task AddModuleConfigAsync(CompanyModuleConfig config, CancellationToken ct = default);
    Task AddSignatureWalletAsync(SignatureWallet wallet, CancellationToken ct = default);
    Task AddWalletMovementAsync(SignatureWalletMovement movement, CancellationToken ct = default);
    Task AddVehicleOwnershipRuleAsync(VehicleOwnershipRule rule, CancellationToken ct = default);
}
