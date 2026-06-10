namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Sesion de verificacion IDSecure. Tabla: identity_verification.verification_sessions.
/// Campos TRA-03 (DocumentTypeCode, Verdict, etc.) se usan en memoria / API wizard hasta mapeo EF completo.
/// </summary>
public sealed class VerificationSession
{
    /// <summary>Pasos 1..4; al completar el 4, <see cref="CurrentStep"/> queda en 4.</summary>
    public const short CompletedStepCount = 4;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ProcedureInstanceId { get; private set; }
    public Guid? VerificationInvitationId { get; private set; }
    public string? ParticipantRole { get; private set; }
    public short CurrentStep { get; private set; }
    public Guid SubjectDocumentTypeId { get; private set; }
    public string SubjectDocumentNumber { get; private set; } = string.Empty;
    public string Provider { get; private set; } = SessionProvider.Mock;
    public string Status { get; private set; } = SessionStatus.Pending;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    /// <summary>TRA-03: codigo tipo documento (CC, NIT) para reutilizacion en memoria.</summary>
    public string? DocumentTypeCode { get; private set; }

    /// <summary>TRA-03: dictamen del proveedor mock.</summary>
    public string? Verdict { get; private set; }

    public decimal? Score { get; private set; }
    public DateTimeOffset? PerformedAt { get; private set; }
    public bool LivenessPerformed { get; private set; }
    public IReadOnlyList<string> ChannelsCompleted { get; private set; } = [];

    private VerificationSession() { }

    /// <summary>
    /// Bootstrap de sesion movil desde invitacion (HU #9481).
    /// subject_document_number queda PENDING hasta captura OCR (HU #9484).
    /// </summary>
    public static VerificationSession BootstrapFromInvitation(
        VerificationInvitation invitation,
        Guid defaultDocumentTypeId,
        DateTimeOffset now)
    {
        return new VerificationSession
        {
            Id = Guid.CreateVersion7(),
            TenantId = invitation.TenantId,
            ProcedureInstanceId = invitation.ProcedureInstanceId,
            VerificationInvitationId = invitation.Id,
            ParticipantRole = invitation.ParticipantRole,
            CurrentStep = 0,
            SubjectDocumentTypeId = defaultDocumentTypeId,
            SubjectDocumentNumber = "PENDING",
            Provider = SessionProvider.Mock,
            Status = SessionStatus.Pending,
            ExpiresAt = invitation.ExpiresAt,
            CreatedAt = now,
            CreatedBy = invitation.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = invitation.CreatedBy,
        };
    }

    /// <summary>HU TRA-03 #9435 — sesion iniciada desde wizard (sin invitacion IDSecure).</summary>
    public static VerificationSession StartForActor(
        Guid tenantId,
        Guid initiatedByUserId,
        string documentTypeCode,
        Guid documentTypeId,
        string documentNumber,
        string provider,
        DateTimeOffset now)
    {
        return new VerificationSession
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SubjectDocumentTypeId = documentTypeId,
            SubjectDocumentNumber = documentNumber,
            DocumentTypeCode = documentTypeCode,
            Provider = provider,
            Status = SessionStatus.Pending,
            CreatedAt = now,
            CreatedBy = initiatedByUserId,
            UpdatedAt = now,
            UpdatedBy = initiatedByUserId,
        };
    }

    /// <summary>TRA-03: aplica resultado del proveedor (canales SMS/email/OTP/liveness).</summary>
    public void ApplyProviderResult(
        string status,
        string verdict,
        decimal score,
        DateTimeOffset performedAt,
        DateTimeOffset expiresAt,
        bool livenessPerformed,
        IReadOnlyList<string> channelsCompleted,
        DateTimeOffset now,
        Guid actorUserId)
    {
        Status = status;
        Verdict = verdict;
        Score = score;
        PerformedAt = performedAt;
        ExpiresAt = expiresAt;
        LivenessPerformed = livenessPerformed;
        ChannelsCompleted = channelsCompleted;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    /// <summary>Avanza el puntero de paso tras completar step_number (1..4).</summary>
    public void AdvanceAfterStepCompleted(short stepNumber, DateTimeOffset now, Guid actorUserId)
    {
        if (stepNumber is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(stepNumber));

        CurrentStep = stepNumber;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    public void ApplySuccessfulOcr(string documentNumber, DateTimeOffset now, Guid actorUserId)
    {
        SubjectDocumentNumber = documentNumber;
        Provider = SessionProvider.VertexAi;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    public void MarkFailed(DateTimeOffset now, Guid actorUserId)
    {
        Status = SessionStatus.Failed;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    public void MarkPassed(DateTimeOffset now, Guid actorUserId)
    {
        Status = SessionStatus.Passed;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    /// <summary>Anonimiza PII del sujeto tras purga de retención (HU #9490 AC1).</summary>
    public void AnonymizeSubjectPii(DateTimeOffset now, Guid actorUserId)
    {
        SubjectDocumentNumber = "REDACTED";
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }
}

public static class SessionStatus
{
    public const string Pending = "pending";
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Expired = "expired";
}

public static class SessionProvider
{
    public const string Mock = "mock";
    public const string VertexAi = "vertex_ai";
}

/// <summary>ID fijo solo para tests/in-memory; en PostgreSQL se resuelve por code CC vía <see cref="Ports.IIdSecureCatalogLookup"/>.</summary>
public static class IdSecureCatalogDefaults
{
    public static readonly Guid DefaultDocumentTypeId =
        Guid.Parse("01900000-000a-7001-8001-000000000001");
}
