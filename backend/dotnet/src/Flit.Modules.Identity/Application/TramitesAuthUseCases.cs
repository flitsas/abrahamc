using System.Security.Cryptography;
using System.Text;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Login / refresh / logout Trámites 2.0 local (HU #9415).</summary>
public static class TramitesAuthUseCases
{
    public const string InvalidCredentialsCode = "INVALID_CREDENTIALS";
    public const string AccountNotActiveCode = "ACCOUNT_NOT_ACTIVE";

    public sealed record LoginCommand(string Email, string Password, string? IpAddress, string? UserAgent);

    public sealed record LoginSuccess(
        TramitesLoginResponse Session,
        string AccessToken,
        DateTimeOffset AccessExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt);

    public enum LoginFailureKind
    {
        InvalidCredentials,
        AccountNotActive,
        TemporarilyLocked,
    }

    public sealed record LoginFailure(
        LoginFailureKind Kind,
        string Code,
        string Message,
        int? RetryAfterSeconds = null);

    public static async Task<Result<LoginSuccess, LoginFailure>> LoginAsync(
        LoginCommand cmd,
        IIdentityAccountRepository accounts,
        IPasswordHasher hasher,
        ITokenIssuer tokenIssuer,
        IClock clock,
        TimeSpan refreshTtl,
        CancellationToken ct = default)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await accounts.FindForAuthAsync(email, ct);

        if (user is null)
        {
            await accounts.RecordLoginAttemptAsync(
                email, succeeded: false, failureReason: "unknown_user",
                tenantId: null, userId: null, cmd.IpAddress, cmd.UserAgent, ct);
            return Fail(LoginFailureKind.InvalidCredentials);
        }

        if (user.AccountState == "permanent_blocked")
        {
            await accounts.RecordLoginAttemptAsync(
                email, succeeded: false, failureReason: "permanent_blocked",
                user.TenantId, user.Id, cmd.IpAddress, cmd.UserAgent, ct);
            return Fail(LoginFailureKind.AccountNotActive);
        }

        if (TramitesAccountLockout.ShouldRehabilitate(user, clock))
        {
            await accounts.RehabilitateAccountAsync(user.Id, clock.UtcNow, ct);
            user = user with { AccountState = "active", BlockedUntil = null };
        }
        else if (TramitesAccountLockout.IsTemporarilyLocked(user, clock))
        {
            await accounts.RecordLoginAttemptAsync(
                email, succeeded: false, failureReason: "temp_blocked",
                user.TenantId, user.Id, cmd.IpAddress, cmd.UserAgent, ct);
            return TramitesAccountLockout.LockedUntil(user.BlockedUntil!.Value, clock);
        }

        if (user.AccountState != "active")
        {
            var reason = user.AccountState switch
            {
                "inactive" => "inactive",
                "temp_blocked" => "temp_blocked",
                _ => "inactive",
            };
            await accounts.RecordLoginAttemptAsync(
                email, succeeded: false, failureReason: reason,
                user.TenantId, user.Id, cmd.IpAddress, cmd.UserAgent, ct);
            return Fail(LoginFailureKind.AccountNotActive);
        }

        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !hasher.Verify(cmd.Password, user.PasswordHash))
        {
            await accounts.RecordLoginAttemptAsync(
                email, succeeded: false, failureReason: "invalid_credentials",
                user.TenantId, user.Id, cmd.IpAddress, cmd.UserAgent, ct);
            await TramitesAccountLockout.MaybeApplyLockoutAfterFailedPasswordAsync(
                user, accounts, clock, ct);

            var reloaded = await accounts.FindForAuthAsync(email, ct);
            if (reloaded is not null &&
                TramitesAccountLockout.IsTemporarilyLocked(reloaded, clock))
            {
                return TramitesAccountLockout.LockedUntil(reloaded.BlockedUntil!.Value, clock);
            }

            return Fail(LoginFailureKind.InvalidCredentials);
        }

        await accounts.ResetFailedAttemptsAsync(user.Id, ct);

        var isSuperAdmin = await accounts.IsSuperAdminAsync(user.Id, ct);
        var slugs = await accounts.GetPermissionSlugsAsync(user.Id, user.TenantId, ct);

        var subject = new TramitesSessionSubject(
            user.Id, user.TenantId, email, isSuperAdmin, slugs);

        var (accessToken, accessExp) = tokenIssuer.IssueTramitesAccess(subject);
        var refreshToken = GenerateOpaqueRefreshToken();
        var refreshHash = HashRefreshToken(refreshToken);
        var refreshExp = clock.UtcNow.Add(refreshTtl);

        await accounts.StoreRefreshTokenAsync(
            user.TenantId, user.Id, refreshHash, refreshExp,
            cmd.UserAgent, cmd.IpAddress, ct);
        await accounts.RecordLoginAttemptAsync(
            email, succeeded: true, failureReason: null,
            user.TenantId, user.Id, cmd.IpAddress, cmd.UserAgent, ct);
        await accounts.TouchLastLoginAsync(user.Id, clock.UtcNow, ct);

        var response = new TramitesLoginResponse(
            user.Id, email, user.TenantId, user.AccountState,
            isSuperAdmin, slugs,
            (int)(accessExp - clock.UtcNow).TotalSeconds);

        return Result<LoginSuccess, LoginFailure>.Success(
            new LoginSuccess(response, accessToken, accessExp, refreshToken, refreshExp));
    }

    public sealed record RefreshCommand(string RefreshToken, string? IpAddress, string? UserAgent);

    public static async Task<Result<LoginSuccess, LoginFailure>> RefreshAsync(
        RefreshCommand cmd,
        IIdentityAccountRepository accounts,
        ITokenIssuer tokenIssuer,
        IClock clock,
        TimeSpan refreshTtl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshToken))
            return Fail(LoginFailureKind.InvalidCredentials);

        var hash = HashRefreshToken(cmd.RefreshToken);
        var row = await accounts.FindRefreshTokenByHashAsync(hash, ct);
        if (row is null || row.RevokedAt is not null || row.ExpiresAt <= clock.UtcNow)
            return Fail(LoginFailureKind.InvalidCredentials);

        var user = await accounts.FindByIdAsync(row.UserId, ct);
        if (user is null || user.AccountState != "active")
            return Fail(LoginFailureKind.AccountNotActive);

        await accounts.RevokeRefreshTokenAsync(hash, clock.UtcNow, ct);

        var email = user.Email;
        var isSuperAdmin = await accounts.IsSuperAdminAsync(user.Id, ct);
        var slugs = await accounts.GetPermissionSlugsAsync(user.Id, user.TenantId, ct);
        var subject = new TramitesSessionSubject(
            user.Id, user.TenantId, email, isSuperAdmin, slugs);

        var (accessToken, accessExp) = tokenIssuer.IssueTramitesAccess(subject);
        var newRefresh = GenerateOpaqueRefreshToken();
        var newHash = HashRefreshToken(newRefresh);
        var refreshExp = clock.UtcNow.Add(refreshTtl);

        await accounts.StoreRefreshTokenAsync(
            user.TenantId, user.Id, newHash, refreshExp,
            cmd.UserAgent, cmd.IpAddress, ct);

        var response = new TramitesLoginResponse(
            user.Id, email, user.TenantId, user.AccountState,
            isSuperAdmin, slugs,
            (int)(accessExp - clock.UtcNow).TotalSeconds);

        return Result<LoginSuccess, LoginFailure>.Success(
            new LoginSuccess(response, accessToken, accessExp, newRefresh, refreshExp));
    }

    public static async Task LogoutAsync(
        string? refreshToken,
        IIdentityAccountRepository accounts,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = HashRefreshToken(refreshToken);
        await accounts.RevokeRefreshTokenAsync(hash, clock.UtcNow, ct);
    }

    private static Result<LoginSuccess, LoginFailure> Fail(LoginFailureKind kind) =>
        // CF-B3 / AC2: mismo mensaje y código para no revelar existencia de cuenta ni estado.
        Result<LoginSuccess, LoginFailure>.Failure(new LoginFailure(
            kind, InvalidCredentialsCode, "Credenciales inválidas."));

    public static string GenerateOpaqueRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }

    public static string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
