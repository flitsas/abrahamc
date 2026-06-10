using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

public interface IVehicleOwnershipRulesRepository
{
    Task<IReadOnlyList<VehicleOwnershipRule>> ListActiveByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<VehicleOwnershipRule?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(VehicleOwnershipRule rule, CancellationToken ct = default);

    void Update(VehicleOwnershipRule rule);
}
