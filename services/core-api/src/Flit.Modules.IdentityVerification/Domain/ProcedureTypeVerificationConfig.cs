namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Mapeo tipo de tramite → participante que exige IDSecure.
/// Tabla: identity_verification.procedure_type_verification_configs.
/// </summary>
public sealed class ProcedureTypeVerificationConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProcedureTypeId { get; private set; }
    public string ParticipantRole { get; private set; } = string.Empty;
    public bool IsRequired { get; private set; }
    public string EmailTemplateKey { get; private set; } = "default";
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private ProcedureTypeVerificationConfig() { }

    public static ProcedureTypeVerificationConfig Create(
        Guid tenantId,
        Guid procedureTypeId,
        string participantRole,
        bool isRequired = true,
        string emailTemplateKey = "default",
        int sortOrder = 0,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantRole);

        return new ProcedureTypeVerificationConfig
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProcedureTypeId = procedureTypeId,
            ParticipantRole = participantRole.Trim(),
            IsRequired = isRequired,
            EmailTemplateKey = emailTemplateKey,
            SortOrder = sortOrder,
            IsActive = isActive,
        };
    }
}
