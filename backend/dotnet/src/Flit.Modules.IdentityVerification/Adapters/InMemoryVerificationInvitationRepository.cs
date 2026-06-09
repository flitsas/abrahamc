using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Repositorio en memoria de invitaciones + plantillas (tests HU #9480).
/// </summary>
public sealed class InMemoryVerificationInvitationRepository : IVerificationInvitationRepository
{
    private readonly ConcurrentDictionary<Guid, VerificationInvitation> _invitations = new();
    private readonly ConcurrentDictionary<string, VerificationEmailTemplate> _templates = new();

    public void SeedInvitation(VerificationInvitation invitation) =>
        _invitations[invitation.Id] = invitation;

    public void SeedTemplate(VerificationEmailTemplate template) =>
        _templates[template.Id.ToString()] = template;

    public Task<VerificationInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        var found = _invitations.Values.FirstOrDefault(i => i.TokenHash == tokenHash);
        return Task.FromResult(found);
    }

    public Task<VerificationInvitation?> GetByIdAsync(Guid tenantId, Guid invitationId, CancellationToken ct)
    {
        if (_invitations.TryGetValue(invitationId, out var inv) && inv.TenantId == tenantId)
            return Task.FromResult<VerificationInvitation?>(inv);
        return Task.FromResult<VerificationInvitation?>(null);
    }

    public Task<VerificationEmailTemplate?> FindEmailTemplateAsync(
        Guid tenantId,
        string templateKey,
        Guid? procedureTypeId,
        CancellationToken ct)
    {
        var found = _templates.Values.FirstOrDefault(t =>
            t.IsActive
            && t.TemplateKey == templateKey
            && (t.TenantId == null || t.TenantId == tenantId)
            && (procedureTypeId is null || t.ProcedureTypeId == procedureTypeId || t.ProcedureTypeId is null));
        return Task.FromResult(found);
    }

    public Task UpdateAsync(VerificationInvitation invitation, CancellationToken ct)
    {
        _invitations[invitation.Id] = invitation;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Captura emails enviados para assertions en tests.</summary>
public sealed class CaptureInvitationEmailSender : IInvitationEmailSender
{
    public sealed record SentEmail(string Recipient, string Subject, string HtmlBody);

    private readonly List<SentEmail> _sent = [];

    public IReadOnlyList<SentEmail> Sent => _sent;

    public Task SendHtmlAsync(string recipientEmail, string subject, string htmlBody, CancellationToken ct)
    {
        _sent.Add(new SentEmail(recipientEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public sealed class ConfigIdSecurePublicUrlProvider(string baseUrl) : IIdSecurePublicUrlProvider
{
    public string GetPublicBaseUrl() => baseUrl;
}
