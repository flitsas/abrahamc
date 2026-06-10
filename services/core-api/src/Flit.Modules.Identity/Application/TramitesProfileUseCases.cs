using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Perfil autoservicio y cambio de contraseña (HU #9420 prereq).</summary>
public static class TramitesProfileUseCases
{
    public const string ProfileNotFoundCode = "PROFILE_NOT_FOUND";
    public const string InvalidCurrentPasswordCode = "INVALID_CURRENT_PASSWORD";
    public const string PasswordNotSetCode = "PASSWORD_NOT_SET";

    public sealed record UpdateProfileCommand(string? FullName, string? Phone, string? Locale);

    public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

    public static async Task<Result<ProfileRow, string>> GetProfileAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        IIdentityProfileRepository profiles,
        CancellationToken ct = default)
    {
        await profiles.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);
        var row = await profiles.GetProfileAsync(tenantId, userId, ct);
        return row is null
            ? Result<ProfileRow, string>.Failure(ProfileNotFoundCode)
            : Result<ProfileRow, string>.Success(row);
    }

    public static async Task<Result<ProfileRow, string>> UpdateProfileAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        UpdateProfileCommand cmd,
        IIdentityProfileRepository profiles,
        CancellationToken ct = default)
    {
        await profiles.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);
        var updated = await profiles.UpdateProfileAsync(
            tenantId, userId, cmd.FullName, cmd.Phone, cmd.Locale, userId, ct);
        if (!updated)
            return Result<ProfileRow, string>.Failure(ProfileNotFoundCode);

        var row = await profiles.GetProfileAsync(tenantId, userId, ct);
        return row is null
            ? Result<ProfileRow, string>.Failure(ProfileNotFoundCode)
            : Result<ProfileRow, string>.Success(row);
    }

    public static async Task<Result<Unit, string>> ChangePasswordAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        ChangePasswordCommand cmd,
        IIdentityProfileRepository profiles,
        IIdentityAccountRepository accounts,
        IIdentityOnboardingRepository onboarding,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        await profiles.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);

        var user = await accounts.FindByIdAsync(userId, ct);
        if (user is null)
            return Result<Unit, string>.Failure(ProfileNotFoundCode);

        if (string.IsNullOrEmpty(user.PasswordHash))
            return Result<Unit, string>.Failure(PasswordNotSetCode);

        if (!hasher.Verify(cmd.CurrentPassword, user.PasswordHash))
            return Result<Unit, string>.Failure(InvalidCurrentPasswordCode);

        var policy = await onboarding.GetPasswordComplexityPolicyAsync(tenantId, ct);
        var policyErrors = TramitesPasswordPolicyValidator.Validate(cmd.NewPassword, policy);
        if (policyErrors.Count > 0)
            return Result<Unit, string>.Failure(TramitesPasswordPolicyValidator.PolicyViolationCode);

        var hash = hasher.Hash(cmd.NewPassword);
        var ok = await profiles.UpdatePasswordAsync(userId, hash, clock.UtcNow, ct);
        return ok
            ? Result<Unit, string>.Success(default)
            : Result<Unit, string>.Failure(ProfileNotFoundCode);
    }
}
