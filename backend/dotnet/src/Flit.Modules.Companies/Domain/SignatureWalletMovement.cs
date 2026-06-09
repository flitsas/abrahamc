namespace Flit.Modules.Companies.Domain;

/// <summary>Ledger append-only (companies.signature_wallet_movements).</summary>
public sealed class SignatureWalletMovement
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WalletId { get; private set; }
    public int Delta { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public int BalanceAfter { get; private set; }
    public Guid? ProcedureInstanceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private SignatureWalletMovement() { }

    public static SignatureWalletMovement Create(
        Guid tenantId,
        Guid walletId,
        int delta,
        string reason,
        int balanceAfter,
        Guid? procedureInstanceId,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || walletId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Identificadores requeridos");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason requerido", nameof(reason));
        ArgumentOutOfRangeException.ThrowIfNegative(balanceAfter);

        return new SignatureWalletMovement
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            WalletId = walletId,
            Delta = delta,
            Reason = reason.Trim(),
            BalanceAfter = balanceAfter,
            ProcedureInstanceId = procedureInstanceId,
            CreatedAt = now,
            CreatedBy = actorUserId,
        };
    }
}
