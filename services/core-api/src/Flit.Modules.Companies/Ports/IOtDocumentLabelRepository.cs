namespace Flit.Modules.Companies.Ports;

public sealed record OtDocumentLabelRecord(
    Guid Id,
    Guid TrafficAgencyId,
    string LabelKey,
    string DisplayName,
    bool ExcludeFromBundle);

public interface IOtDocumentLabelRepository
{
    Task<IReadOnlyList<OtDocumentLabelRecord>> ListByAgencyAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default);

    Task<OtDocumentLabelRecord> CreateAsync(
        Guid trafficAgencyId,
        string labelKey,
        string displayName,
        bool excludeFromBundle,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<bool> SoftDeleteAsync(
        Guid trafficAgencyId,
        Guid labelId,
        Guid actorUserId,
        CancellationToken ct = default);
}
