using Flit.Modules.Identity.Application;
using Flit.Modules.Notifications.Domain;
using Flit.Modules.Notifications.Ports;

namespace Flit.Infrastructure.Adapters;

/// <summary>Envía invitación de onboarding vía servicio SMTP global (#9549 HU #9682).</summary>
public sealed class SmtpOnboardingEmailNotifier(IGlobalEmailService globalEmail) : IOnboardingEmailNotifier
{
    public Task SendInvitationAsync(
        string email,
        string activationUrl,
        DateTimeOffset expiresAt,
        CancellationToken ct = default) =>
        globalEmail.SendTemplatedAsync(
            email,
            EmailTemplateKeys.OnboardingInvitation,
            new Dictionary<string, string>
            {
                ["activation_url"] = activationUrl,
                ["expires_at"] = expiresAt.ToString("f"),
            },
            ct: ct);
}
