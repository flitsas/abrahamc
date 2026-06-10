namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Invitacion IDSecure por participante. Tabla: identity_verification.verification_invitations.
/// El token en claro se envia por email; solo token_hash se persiste.
/// </summary>
public sealed class VerificationInvitation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProcedureInstanceId { get; private set; }
    public Guid? ProcedureActorId { get; private set; }
    public string ParticipantRole { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = InvitationStatus.Pending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? LastSentAt { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    private VerificationInvitation() { }

    public static VerificationInvitation CreatePending(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid? procedureActorId,
        string participantRole,
        string recipientEmail,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? idempotencyKey,
        Guid actorUserId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new VerificationInvitation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProcedureInstanceId = procedureInstanceId,
            ProcedureActorId = procedureActorId,
            ParticipantRole = participantRole.Trim(),
            RecipientEmail = recipientEmail.Trim().ToLowerInvariant(),
            TokenHash = tokenHash,
            Status = InvitationStatus.Pending,
            ExpiresAt = expiresAt,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsConsumed => Status == InvitationStatus.Consumed;

    public void MarkSent(DateTimeOffset now, Guid actorUserId)
    {
        if (Status is not (InvitationStatus.Pending or InvitationStatus.Sent))
            throw new InvalidOperationException($"No se puede enviar email en estado {Status}");

        Status = InvitationStatus.Sent;
        LastSentAt = now;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    /// <summary>Primer acceso a la URL — token de un solo uso (HU #9480 AC2).</summary>
    public void MarkConsumed(DateTimeOffset now, Guid actorUserId)
    {
        if (Status == InvitationStatus.Consumed)
            throw new InvitationAlreadyConsumedException();

        if (IsExpired(now))
            throw new InvitationExpiredException();

        Status = InvitationStatus.Consumed;
        ConsumedAt = now;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }
}

public static class InvitationStatus
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Opened = "opened";
    public const string Consumed = "consumed";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
}

public sealed class InvitationAlreadyConsumedException : Exception
{
    public InvitationAlreadyConsumedException()
        : base("Token de invitacion ya consumido") { }
}

public sealed class InvitationExpiredException : Exception
{
    public InvitationExpiredException()
        : base("Token de invitacion expirado") { }
}
