using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

/// <summary>Puerto CRUD para ot.ot_rules (HU #9456 OT-03).</summary>
public interface IOtRuleRepository
{
    Task<OtRule?> GetByIdAsync(Guid id, Guid trafficAgencyId, CancellationToken ct = default);
    Task<IReadOnlyList<OtRule>> ListByAgencyAsync(Guid trafficAgencyId, CancellationToken ct = default);

    Task<IReadOnlyList<OtRule>> ListActiveByTriggerAsync(
        Guid trafficAgencyId,
        string triggerEvent,
        CancellationToken ct = default);

    Task AddAsync(OtRule rule, CancellationToken ct = default);
    Task UpdateAsync(OtRule rule, CancellationToken ct = default);
}
