using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Lectura/escritura de invitaciones y plantillas email IDSecure (HU #9480).
/// </summary>
public interface IVerificationInvitationRepository
{
    Task<VerificationInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct);

    Task<VerificationInvitation?> GetByIdAsync(Guid tenantId, Guid invitationId, CancellationToken ct);

    Task<VerificationEmailTemplate?> FindEmailTemplateAsync(
        Guid tenantId,
        string templateKey,
        Guid? procedureTypeId,
        CancellationToken ct);

    Task UpdateAsync(VerificationInvitation invitation, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Envio SMTP HTML de invitacion IDSecure.</summary>
public interface IInvitationEmailSender
{
    Task SendHtmlAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken ct);
}

/// <summary>URL publica base para enlaces de validacion (frontend o API).</summary>
public interface IIdSecurePublicUrlProvider
{
    string GetPublicBaseUrl();
}
