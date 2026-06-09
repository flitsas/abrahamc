using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Bloqueos temporales y rehabilitación automática (HU #9416).</summary>
public static class TramitesAccountLockout
{
    public const string AccountLockedCode = "ACCOUNT_LOCKED";

    public static bool IsTemporarilyLocked(AuthUserRow user, IClock clock) =>
        user.AccountState == "temp_blocked" &&
        user.BlockedUntil is { } until &&
        until > clock.UtcNow;

    public static bool ShouldRehabilitate(AuthUserRow user, IClock clock) =>
        user.AccountState == "temp_blocked" &&
        user.BlockedUntil is { } until &&
        until <= clock.UtcNow;

    public static Result<TramitesAuthUseCases.LoginSuccess, TramitesAuthUseCases.LoginFailure> LockedUntil(
        DateTimeOffset blockedUntil,
        IClock clock) =>
        Result<TramitesAuthUseCases.LoginSuccess, TramitesAuthUseCases.LoginFailure>.Failure(
            BuildLockedFailure(blockedUntil, clock));

    public static TramitesAuthUseCases.LoginFailure BuildLockedFailure(
        DateTimeOffset blockedUntil,
        IClock clock)
    {
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((blockedUntil - clock.UtcNow).TotalSeconds));
        var minutes = Math.Max(1, (int)Math.Ceiling(retryAfterSeconds / 60.0));
        return new TramitesAuthUseCases.LoginFailure(
            TramitesAuthUseCases.LoginFailureKind.TemporarilyLocked,
            AccountLockedCode,
            $"Cuenta bloqueada temporalmente. Intente de nuevo en {minutes} minuto(s).",
            retryAfterSeconds);
    }

    public static async Task MaybeApplyLockoutAfterFailedPasswordAsync(
        AuthUserRow user,
        IIdentityAccountRepository accounts,
        IClock clock,
        CancellationToken ct)
    {
        var policy = await accounts.GetPasswordPolicyAsync(user.TenantId, ct);
        var failedCount = await accounts.IncrementFailedAttemptsAsync(user.Id, ct);
        if (failedCount < policy.LockoutThreshold)
            return;

        var blockedUntil = clock.UtcNow.AddMinutes(policy.LockoutMinutes);
        await accounts.ApplyTempBlockAsync(
            user.Id, blockedUntil, "lockout_exceeded", clock.UtcNow, ct);
    }
}
