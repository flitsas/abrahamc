using Flit.Modules.Identity.Application;

namespace Flit.Infrastructure.Adapters;

/// <summary>Sin envío SMTP; el enlace de activación se devuelve en la respuesta API (DEV/QA).</summary>
public sealed class NoOpOnboardingEmailNotifier : IOnboardingEmailNotifier
{
    public Task SendInvitationAsync(
        string email,
        string activationUrl,
        DateTimeOffset expiresAt,
        CancellationToken ct = default) =>
        Task.CompletedTask;
}
