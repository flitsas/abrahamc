namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Progreso secuencial del stepper. Tabla: verification_session_steps (step_number 1..4).
/// </summary>
public sealed class VerificationSessionStep
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VerificationSessionId { get; private set; }
    public short StepNumber { get; private set; }
    public string StepCode { get; private set; } = string.Empty;
    public string Status { get; private set; } = StepStatus.Pending;
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    private VerificationSessionStep() { }

    public static VerificationSessionStep CreatePending(
        VerificationSession session,
        short stepNumber,
        string stepCode,
        DateTimeOffset now)
    {
        if (stepNumber is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(stepNumber));

        return new VerificationSessionStep
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            VerificationSessionId = session.Id,
            StepNumber = stepNumber,
            StepCode = stepCode,
            Status = StepStatus.Pending,
            CreatedAt = now,
            CreatedBy = session.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = session.CreatedBy,
        };
    }

    public static IReadOnlyList<VerificationSessionStep> CreateDefaultSequence(
        VerificationSession session,
        DateTimeOffset now) =>
    [
        CreatePending(session, 1, StepCodes.DocumentCapture, now),
        CreatePending(session, 2, StepCodes.Selfie, now),
        CreatePending(session, 3, StepCodes.Liveness, now),
        CreatePending(session, 4, StepCodes.Signature, now),
    ];

    public void MarkCompleted(DateTimeOffset now, Guid actorUserId)
    {
        Status = StepStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    public void MarkFailed(string failureReason, DateTimeOffset now, Guid actorUserId)
    {
        Status = StepStatus.Failed;
        MetadataJson = OcrStepMetadata.BuildFailureJson(failureReason);
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    /// <summary>JSON metadata del paso (columna metadata — AC2).</summary>
    public string MetadataJson { get; private set; } = "{}";
}

public static class OcrStepMetadata
{
    public static string BuildFailureJson(string reason) =>
        $$"""{"{{OcrFailureMetadataKeys.Reason}}":"{{reason.Replace("\"", "\\\"")}}"}""";
}

public static class StepStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

public static class StepCodes
{
    public const string DocumentCapture = "document_capture";
    public const string Selfie = "selfie";
    public const string Liveness = "liveness";
    public const string Signature = "signature";
}
