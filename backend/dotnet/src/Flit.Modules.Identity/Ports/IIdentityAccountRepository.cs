using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Acceso a identity.users y sesión Trámites 2.0 (HU #9415).</summary>
public interface IIdentityAccountRepository
{
    Task<AuthUserRow?> FindForAuthAsync(string email, CancellationToken ct = default);

    Task<AuthUserRow?> FindByIdAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetPermissionSlugsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Unión aditiva de roles y permisos del usuario en el tenant (#9683).</summary>
    Task<UserAuthContext> GetAuthContextAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    Task<int> GetPermissionsEpochAsync(Guid userId, CancellationToken ct = default);

    Task<int> BumpPermissionsEpochAsync(Guid userId, CancellationToken ct = default);

    Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken ct = default);

    Task RecordLoginAttemptAsync(
        string email,
        bool succeeded,
        string? failureReason,
        Guid? tenantId,
        Guid? userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task TouchLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken ct = default);

    Task StoreRefreshTokenAsync(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct = default);

    Task<RefreshTokenRow?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(string tokenHash, DateTimeOffset revokedAt, CancellationToken ct = default);

    Task<PasswordPolicyRow> GetPasswordPolicyAsync(Guid tenantId, CancellationToken ct = default);

    Task<int> IncrementFailedAttemptsAsync(Guid userId, CancellationToken ct = default);

    Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct = default);

    Task ApplyTempBlockAsync(
        Guid userId,
        DateTimeOffset blockedUntil,
        string blockReason,
        DateTimeOffset at,
        CancellationToken ct = default);

    Task RehabilitateAccountAsync(Guid userId, DateTimeOffset at, CancellationToken ct = default);
}

public sealed record RefreshTokenRow(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);
