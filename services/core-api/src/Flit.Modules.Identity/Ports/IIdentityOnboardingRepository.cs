using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Onboarding criptográfico y política de contraseña (HU #9418).</summary>
public interface IIdentityOnboardingRepository
{
    Task<PasswordComplexityPolicyRow> GetPasswordComplexityPolicyAsync(
        Guid tenantId, CancellationToken ct = default);

    Task<OnboardingInvitationRow?> FindInvitationByTokenHashAsync(
        string tokenHash, CancellationToken ct = default);

    Task<bool> HasPendingInvitationAsync(
        Guid tenantId, string email, CancellationToken ct = default);

    Task<bool> HasActiveUserWithEmailAsync(string email, CancellationToken ct = default);

    Task<Guid?> FindInactiveUserIdByEmailAsync(
        Guid tenantId, string email, CancellationToken ct = default);

    Task<Guid> CreateInactiveUserAsync(
        Guid tenantId,
        string email,
        Guid createdBy,
        CancellationToken ct = default);

    Task<OnboardingInvitationRow> CreateInvitationAsync(
        Guid invitationId,
        Guid tenantId,
        string email,
        Guid invitedRoleId,
        string tokenHash,
        string signature,
        DateTimeOffset expiresAt,
        Guid createdBy,
        CancellationToken ct = default);

    Task RevokePendingInvitationsAsync(
        Guid tenantId, string email, Guid updatedBy, CancellationToken ct = default);

    Task MarkInvitationExpiredAsync(Guid invitationId, Guid updatedBy, CancellationToken ct = default);

    Task ConsumeInvitationAsync(
        Guid invitationId,
        DateTimeOffset consumedAt,
        Guid updatedBy,
        CancellationToken ct = default);

    Task ActivateUserPasswordAsync(
        Guid userId,
        string passwordHash,
        DateTimeOffset at,
        CancellationToken ct = default);

    Task EnsureUserRoleAsync(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid createdBy,
        CancellationToken ct = default);

    Task ApplyTenantGucAsync(Guid tenantId, CancellationToken ct = default);

    Task<Guid?> FindUserIdByEmailInTenantAsync(
        Guid tenantId, string email, CancellationToken ct = default);

    Task<bool> RoleExistsAsync(Guid roleId, CancellationToken ct = default);
}
