namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Enlace instancia de trámite ↔ sesión IDSecure. Tabla: procedures.procedure_identity_validations (HU #9486).
/// </summary>
public sealed class ProcedureIdentityValidation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProcedureInstanceId { get; private set; }
    public Guid VerificationSessionId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Verdict { get; private set; } = IdentityValidationVerdict.Pending;
    public DateTimeOffset LinkedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public int RowVersion { get; private set; } = 1;

    private ProcedureIdentityValidation() { }

    public static ProcedureIdentityValidation LinkSession(
        VerificationSession session,
        VerificationInvitation invitation,
        string verdict,
        DateTimeOffset now)
    {
        return new ProcedureIdentityValidation
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            ProcedureInstanceId = session.ProcedureInstanceId
                ?? throw new InvalidOperationException("Sesión sin procedure_instance_id"),
            VerificationSessionId = session.Id,
            ActorId = invitation.ProcedureActorId,
            Verdict = verdict,
            LinkedAt = now,
            CreatedAt = now,
            CreatedBy = session.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = session.CreatedBy,
        };
    }

    /// <summary>HU TRA-03 #9435 — vincula sesion reutilizada o nueva a instancia de tramite.</summary>
    public static ProcedureIdentityValidation LinkForActor(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid verificationSessionId,
        Guid? actorId,
        string? verdict,
        DateTimeOffset now,
        Guid createdBy)
    {
        return new ProcedureIdentityValidation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProcedureInstanceId = procedureInstanceId,
            VerificationSessionId = verificationSessionId,
            ActorId = actorId,
            Verdict = verdict ?? IdentityValidationVerdict.Pending,
            LinkedAt = now,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
        };
    }

    public void UpdateVerdict(string verdict, DateTimeOffset now, Guid actorUserId)
    {
        Verdict = verdict;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }
}

public static class IdentityValidationVerdict
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class CrossMatchFailureCodes
{
    public const string CfI5DocumentMismatch = "CF-I5";
}
