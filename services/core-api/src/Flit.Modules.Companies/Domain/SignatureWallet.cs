namespace Flit.Modules.Companies.Domain;

public sealed class SignatureWallet
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int Balance { get; private set; }
    public int LowThreshold { get; private set; }
    public bool AutoRecharge { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public int RowVersion { get; private set; }

    private SignatureWallet() { }

    public static SignatureWallet Create(
        Guid tenantId,
        int balance,
        int lowThreshold,
        bool autoRecharge,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido", nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfNegative(balance);
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        return new SignatureWallet
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Balance = balance,
            LowThreshold = lowThreshold,
            AutoRecharge = autoRecharge,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        };
    }
}
