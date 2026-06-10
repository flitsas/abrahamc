namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Override manual auditado. Tabla: verification_manual_overrides (HU #9487).
/// </summary>
public sealed class VerificationManualOverride
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VerificationSessionId { get; private set; }
    public string PreviousVerdict { get; private set; } = string.Empty;
    public string NewVerdict { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid OverriddenBy { get; private set; }
    public DateTimeOffset OverriddenAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public int RowVersion { get; private set; } = 1;

    private VerificationManualOverride() { }

    public static VerificationManualOverride Create(
        VerificationSession session,
        string previousVerdict,
        string newVerdict,
        string reason,
        Guid operatorUserId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (newVerdict is not (ManualOverrideVerdict.Approved or ManualOverrideVerdict.Rejected))
            throw new ArgumentException("new_verdict debe ser approved o rejected", nameof(newVerdict));

        return new VerificationManualOverride
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            VerificationSessionId = session.Id,
            PreviousVerdict = previousVerdict,
            NewVerdict = newVerdict,
            Reason = reason.Trim(),
            OverriddenBy = operatorUserId,
            OverriddenAt = now,
            CreatedAt = now,
            CreatedBy = operatorUserId,
        };
    }
}

public static class ManualOverrideVerdict
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class IdSecurePermissions
{
    public const string Review = "idsecure.review";
    public const string Analytics = "idsecure.analytics";
}

public static class ReviewPanelBuckets
{
    public const string Sent = "sent";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <summary>Entrada append-only en audit.audit_log (DEV in-memory).</summary>
public sealed class IdentityVerificationAuditEntry
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string SchemaName { get; init; } = "identity_verification";
    public string TableName { get; init; } = string.Empty;
    public Guid RecordId { get; init; }
    public char Operation { get; init; } = 'I';
    public Guid ChangedBy { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string? NewValuesJson { get; init; }
}
