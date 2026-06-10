namespace Flit.Modules.Identity.Ports;

public sealed record PasswordResetTokenRow(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    DateTimeOffset ExpiresAt);

/// <summary>Tokens de restablecimiento en identity.password_reset_tokens (HU #9684).</summary>
public interface IIdentityPasswordResetRepository
{
    Task CreateTokenAsync(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    Task<PasswordResetTokenRow?> FindValidByHashAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task ConsumeAndUpdatePasswordAsync(
        Guid tokenId,
        Guid userId,
        string passwordHash,
        DateTimeOffset at,
        CancellationToken ct = default);
}
