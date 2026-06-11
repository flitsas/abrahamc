using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Invitaciones firmadas y activación de cuenta (HU #9684).</summary>
public static class TramitesOnboardingUseCases
{
    public const string InvitationExpiredCode = "INVITATION_EXPIRED";
    public const string InvitationInvalidCode = "INVITATION_INVALID";
    public const string InvitationConsumedCode = "INVITATION_CONSUMED";
    public const string EmailExistsCode = "EMAIL_ALREADY_REGISTERED";
    public const string RoleNotFoundCode = "ROLE_NOT_FOUND";
    public const string ForbiddenTenantCode = "TENANT_MISMATCH";

    public sealed record CreateInvitationCommand(
        string Email,
        Guid InvitedRoleId,
        Guid? TargetTenantId);

    public sealed record CreateInvitationSuccess(
        Guid InvitationId,
        string Email,
        DateTimeOffset ExpiresAt,
        string ActivationUrl,
        bool EmailQueued);

    public sealed record PreviewInvitationQuery(
        Guid InvitationId,
        string Token,
        string Signature);

    public sealed record PreviewInvitationSuccess(
        string Email,
        Guid TenantId,
        string TenantName,
        DateTimeOffset ExpiresAt,
        PasswordComplexityPolicyRow Policy);

    public sealed record ActivateAccountCommand(
        Guid InvitationId,
        string Token,
        string Signature,
        string Password);

    public sealed record ActivateAccountSuccess(Guid UserId, string Email);

    public static async Task<Result<CreateInvitationSuccess, string>> CreateInvitationAsync(
        CreateInvitationCommand cmd,
        Guid actorTenantId,
        bool actorIsSuperAdmin,
        Guid actorUserId,
        IIdentityOnboardingRepository onboarding,
        IGlobalAuthSettingsReader authSettings,
        byte[] signingKey,
        string activationBaseUrl,
        IOnboardingEmailNotifier? notifier,
        IClock clock,
        CancellationToken ct = default)
    {
        var tenantId = cmd.TargetTenantId ?? actorTenantId;
        if (!actorIsSuperAdmin && tenantId != actorTenantId)
            return Result<CreateInvitationSuccess, string>.Failure(ForbiddenTenantCode);

        var email = cmd.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || !email.Contains('@', StringComparison.Ordinal))
            return Result<CreateInvitationSuccess, string>.Failure("INVALID_EMAIL");

        await onboarding.ApplyTenantGucAsync(tenantId, ct);

        if (!await onboarding.RoleExistsAsync(cmd.InvitedRoleId, ct))
            return Result<CreateInvitationSuccess, string>.Failure(RoleNotFoundCode);

        if (await onboarding.HasActiveUserWithEmailAsync(email, ct))
            return Result<CreateInvitationSuccess, string>.Failure(EmailExistsCode);
        await onboarding.RevokePendingInvitationsAsync(tenantId, email, actorUserId, ct);

        _ = await onboarding.FindInactiveUserIdByEmailAsync(tenantId, email, ct)
            ?? await onboarding.CreateInactiveUserAsync(tenantId, email, actorUserId, ct);

        var tokenSettings = await authSettings.GetAsync(ct);
        var invitationId = Guid.CreateVersion7();
        var token = TramitesOnboardingToken.GenerateToken();
        var tokenHash = TramitesOnboardingToken.HashToken(token);
        var expiresAt = TramitesOnboardingToken.NormalizeExpiresAtForStorage(
            clock.UtcNow.Add(tokenSettings.InvitationTtl));
        var signature = TramitesOnboardingToken.ComputeSignature(
            invitationId, token, expiresAt, signingKey);

        var row = await onboarding.CreateInvitationAsync(
            invitationId, tenantId, email, cmd.InvitedRoleId, tokenHash, signature, expiresAt, actorUserId, ct);

        var activationUrl = TramitesOnboardingToken.BuildActivationUrl(
            activationBaseUrl, row.Id, token, signature);

        var emailQueued = false;
        if (notifier is not null)
        {
            try
            {
                await notifier.SendInvitationAsync(email, activationUrl, expiresAt, ct);
                emailQueued = true;
            }
            catch
            {
                // La invitación queda creada; activationUrl sigue en la respuesta API.
                emailQueued = false;
            }
        }

        return Result<CreateInvitationSuccess, string>.Success(
            new CreateInvitationSuccess(row.Id, email, expiresAt, activationUrl, emailQueued));
    }

    public static async Task<Result<PreviewInvitationSuccess, string>> PreviewInvitationAsync(
        PreviewInvitationQuery query,
        IIdentityOnboardingRepository onboarding,
        byte[] signingKey,
        IClock clock,
        CancellationToken ct = default)
    {
        var resolved = await ResolveInvitationAsync(query, onboarding, signingKey, clock, ct);
        return resolved.Match(
            ok => Result<PreviewInvitationSuccess, string>.Success(
                new PreviewInvitationSuccess(
                    ok.Row.Email,
                    ok.Row.TenantId,
                    ok.Row.TenantId.ToString(),
                    ok.Row.ExpiresAt,
                    ok.Policy)),
            err => Result<PreviewInvitationSuccess, string>.Failure(err));
    }

    public static async Task<Result<ActivateAccountSuccess, string>> ActivateAccountAsync(
        ActivateAccountCommand cmd,
        IIdentityOnboardingRepository onboarding,
        IPasswordHasher hasher,
        byte[] signingKey,
        IClock clock,
        CancellationToken ct = default)
    {
        var previewQuery = new PreviewInvitationQuery(cmd.InvitationId, cmd.Token, cmd.Signature);
        var resolved = await ResolveInvitationAsync(previewQuery, onboarding, signingKey, clock, ct);

        return await resolved.Match(
            async ok =>
            {
                var policyErrors = TramitesPasswordPolicyValidator.Validate(cmd.Password, ok.Policy);
                if (policyErrors.Count > 0)
                    return Result<ActivateAccountSuccess, string>.Failure(
                        TramitesPasswordPolicyValidator.PolicyViolationCode);

                var row = ok.Row;
                var userId = await onboarding.FindInactiveUserIdByEmailAsync(row.TenantId, row.Email, ct)
                    ?? await onboarding.FindUserIdByEmailInTenantAsync(row.TenantId, row.Email, ct);

                if (userId is null)
                    return Result<ActivateAccountSuccess, string>.Failure(InvitationInvalidCode);

                var passwordHash = hasher.Hash(cmd.Password);
                await onboarding.ActivateUserPasswordAsync(userId.Value, passwordHash, clock.UtcNow, ct);
                await onboarding.EnsureUserRoleAsync(
                    row.TenantId, userId.Value, row.InvitedRoleId, userId.Value, ct);
                await onboarding.ConsumeInvitationAsync(row.Id, clock.UtcNow, userId.Value, ct);

                return Result<ActivateAccountSuccess, string>.Success(
                    new ActivateAccountSuccess(userId.Value, row.Email));
            },
            err => Task.FromResult(Result<ActivateAccountSuccess, string>.Failure(err)));
    }

    private sealed record ResolvedInvitation(OnboardingInvitationRow Row, PasswordComplexityPolicyRow Policy);

    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    private static async Task<Result<ResolvedInvitation, string>> ResolveInvitationAsync(
        PreviewInvitationQuery query,
        IIdentityOnboardingRepository onboarding,
        byte[] signingKey,
        IClock clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Token))
            return Result<ResolvedInvitation, string>.Failure(InvitationInvalidCode);

        var tokenHash = TramitesOnboardingToken.HashToken(query.Token);
        var row = await onboarding.FindInvitationByTokenHashAsync(tokenHash, ct);
        if (row is null || row.Id != query.InvitationId)
            return Result<ResolvedInvitation, string>.Failure(InvitationInvalidCode);

        if (!TramitesOnboardingToken.VerifyStoredSignature(row.Signature, query.Signature)
            && !TramitesOnboardingToken.VerifySignature(
                row.Id, query.Token, row.ExpiresAt, query.Signature, signingKey))
            return Result<ResolvedInvitation, string>.Failure(InvitationInvalidCode);

        if (row.Status == "consumed")
            return Result<ResolvedInvitation, string>.Failure(InvitationConsumedCode);

        if (row.Status is "revoked" or "expired")
            return Result<ResolvedInvitation, string>.Failure(InvitationExpiredCode);

        if (row.ExpiresAt <= clock.UtcNow)
        {
            await onboarding.ApplyTenantGucAsync(row.TenantId, ct);
            await onboarding.MarkInvitationExpiredAsync(row.Id, SystemUserId, ct);
            return Result<ResolvedInvitation, string>.Failure(InvitationExpiredCode);
        }

        if (row.Status != "pending")
            return Result<ResolvedInvitation, string>.Failure(InvitationInvalidCode);

        await onboarding.ApplyTenantGucAsync(row.TenantId, ct);
        var policy = await onboarding.GetPasswordComplexityPolicyAsync(row.TenantId, ct);

        return Result<ResolvedInvitation, string>.Success(
            new ResolvedInvitation(row, policy));
    }
}

/// <summary>Stub de mensajería — en DEV registra el enlace (correo real en infra futura).</summary>
public interface IOnboardingEmailNotifier
{
    Task SendInvitationAsync(
        string email, string activationUrl, DateTimeOffset expiresAt, CancellationToken ct = default);
}
