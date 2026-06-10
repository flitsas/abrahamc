using Flit.SharedKernel;

namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Orden activo del expediente consolidado por OT (ot.ot_consolidated_doc_orders — Feature #9379).
/// </summary>
public sealed class OtConsolidatedDocOrder
{
    public Guid Id { get; private set; }
    public Guid TrafficAgencyId { get; private set; }
    public int Version { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public int RowVersion { get; private set; }

    private OtConsolidatedDocOrder() { }

    public bool IsDeleted => DeletedAt.HasValue;

    public static OtConsolidatedDocOrder Create(
        Guid trafficAgencyId,
        Guid actorUserId,
        DateTimeOffset now)
    {
        return new OtConsolidatedDocOrder
        {
            Id = Guid.NewGuid(),
            TrafficAgencyId = trafficAgencyId,
            Version = 1,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        };
    }

    public void Touch(Guid actorUserId, DateTimeOffset now)
    {
        UpdatedBy = actorUserId;
        UpdatedAt = now;
        RowVersion++;
    }
}
