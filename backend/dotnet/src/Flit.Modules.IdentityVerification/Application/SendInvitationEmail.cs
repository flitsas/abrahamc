using System.Text;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9480 AC1 — envia correo SMTP HTML con URL de token (48h TTL).
/// </summary>
public static class SendInvitationEmail
{
    public sealed record Command(
        Guid TenantId,
        Guid InvitationId,
        string PlainToken,
        string EmailTemplateKey,
        Guid? ProcedureTypeId,
        Guid ActorUserId);

    public sealed record Response(
        Guid InvitationId,
        string ValidationUrl,
        DateTimeOffset ExpiresAt);

    public abstract record SendInvitationEmailError(string Code, string Message)
    {
        public sealed record NotFound()
            : SendInvitationEmailError("INVITATION_NOT_FOUND", "Invitacion no encontrada");

        public sealed record InvalidState(string Status)
            : SendInvitationEmailError("INVALID_STATE", $"Estado invalido para envio: {Status}");

        public sealed record TemplateNotFound(string Key)
            : SendInvitationEmailError("TEMPLATE_NOT_FOUND", $"Plantilla email no encontrada: {Key}");
    }

    public static async Task<Result<Response, SendInvitationEmailError>> HandleAsync(
        Command cmd,
        IVerificationInvitationRepository repo,
        IInvitationEmailSender emailSender,
        IIdSecurePublicUrlProvider urlProvider,
        IClock clock,
        CancellationToken ct = default)
    {
        var invitation = await repo.GetByIdAsync(cmd.TenantId, cmd.InvitationId, ct);
        if (invitation is null)
            return Result<Response, SendInvitationEmailError>.Failure(new SendInvitationEmailError.NotFound());

        if (invitation.Status is not (InvitationStatus.Pending or InvitationStatus.Sent))
            return Result<Response, SendInvitationEmailError>.Failure(
                new SendInvitationEmailError.InvalidState(invitation.Status));

        var template = await repo.FindEmailTemplateAsync(
                cmd.TenantId, cmd.EmailTemplateKey, cmd.ProcedureTypeId, ct)
            ?? await repo.FindEmailTemplateAsync(cmd.TenantId, "default", null, ct);

        if (template is null)
            template = VerificationEmailTemplate.CreateDefault(cmd.EmailTemplateKey);

        var validationUrl = InvitationEmailTemplateRenderer.BuildValidationUrl(
            urlProvider.GetPublicBaseUrl(), cmd.PlainToken);

        var (subject, html) = InvitationEmailTemplateRenderer.Render(
            template.Subject,
            template.HtmlBody,
            validationUrl,
            invitation.ExpiresAt);

        await emailSender.SendHtmlAsync(invitation.RecipientEmail, subject, html, ct);

        var now = clock.UtcNow;
        invitation.MarkSent(now, cmd.ActorUserId);
        await repo.UpdateAsync(invitation, ct);
        await repo.SaveChangesAsync(ct);

        return Result<Response, SendInvitationEmailError>.Success(
            new Response(invitation.Id, validationUrl, invitation.ExpiresAt));
    }
}
