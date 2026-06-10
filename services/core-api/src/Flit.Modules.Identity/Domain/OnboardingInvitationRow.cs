namespace Flit.Modules.Identity.Domain;

/// <summary>Fila de identity.onboarding_invitations (HU #9418).</summary>
public sealed record OnboardingInvitationRow(
    Guid Id,
    Guid TenantId,
    string Email,
    Guid InvitedRoleId,
    string TokenHash,
    string Signature,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt);
