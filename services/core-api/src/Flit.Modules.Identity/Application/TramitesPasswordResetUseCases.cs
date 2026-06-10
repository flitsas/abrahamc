using System.Security.Cryptography;
using System.Text;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Olvido y restablecimiento de contraseña Trámites 2.0 (HU #9684 / #9685).</summary>
public static class TramitesPasswordResetUseCases
{
    public const string InvalidTokenCode = "INVALID_RESET_TOKEN";
    public const string PolicyViolationCode = TramitesPasswordPolicyValidator.PolicyViolationCode;

    public static readonly TimeSpan ResetTtl = TimeSpan.FromMinutes(30);

    public sealed record RequestCommand(string Email, string? IpAddress, string? UserAgent);

    public sealed record RequestSuccess(string RecipientEmail, string RawToken, DateTimeOffset ExpiresAt);

    public sealed record ConfirmCommand(string ResetToken, string NewPassword);

    public static async Task<RequestSuccess?> RequestAsync(
        RequestCommand cmd,
        IIdentityAccountRepository accounts,
        IIdentityPasswordResetRepository resets,
        IClock clock,
        CancellationToken ct = default)
    {
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
            return null;

        var user = await accounts.FindForAuthAsync(normalizedEmail, ct);
        if (user is null || user.AccountState is not "active")
            return null;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = HashToken(rawToken);
        var expiresAt = clock.UtcNow.Add(ResetTtl);

        await resets.CreateTokenAsync(
            user.TenantId, user.Id, tokenHash, expiresAt, ct);

        return new RequestSuccess(user.Email, rawToken, expiresAt);
    }

    public static async Task<Result<Unit, string>> ConfirmAsync(
        ConfirmCommand cmd,
        IIdentityAccountRepository accounts,
        IIdentityOnboardingRepository onboarding,
        IIdentityPasswordResetRepository resets,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.ResetToken))
            return Result<Unit, string>.Failure(InvalidTokenCode);

        var tokenHash = HashToken(cmd.ResetToken.Trim());
        var row = await resets.FindValidByHashAsync(tokenHash, clock.UtcNow, ct);
        if (row is null)
            return Result<Unit, string>.Failure(InvalidTokenCode);

        var user = await accounts.FindByIdAsync(row.UserId, ct);
        if (user is null || user.AccountState is not "active")
            return Result<Unit, string>.Failure(InvalidTokenCode);

        await onboarding.ApplyTenantGucAsync(user.TenantId, ct);
        var policyRow = await onboarding.GetPasswordComplexityPolicyAsync(user.TenantId, ct);

        if (TramitesPasswordPolicyValidator.Validate(cmd.NewPassword, policyRow).Count > 0)
            return Result<Unit, string>.Failure(PolicyViolationCode);

        var passwordHash = hasher.Hash(cmd.NewPassword);
        await resets.ConsumeAndUpdatePasswordAsync(
            row.Id, user.Id, passwordHash, clock.UtcNow, ct);

        return Result<Unit, string>.Success(default);
    }

    public static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
