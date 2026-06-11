using System.Security.Cryptography;
using System.Text;
using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;
using IClock = Flit.SharedKernel.IClock;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;

namespace Flit.Api.Endpoints;

/// <summary>Onboarding criptográfico y política de contraseña (HU #9418).</summary>
public static class TramitesOnboardingEndpoints
{
    public sealed record CreateInvitationRequest(
        string Email,
        Guid InvitedRoleId,
        Guid? TenantId);

    public sealed record ActivateAccountRequest(
        Guid InvitationId,
        string Token,
        string Signature,
        string Password);

    public static void MapTramitesOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity/onboarding")
            .WithTags("Identity - Onboarding");

        group.MapPost("/invitations", async (
            CreateInvitationRequest req,
            HttpContext http,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            IIdentityOnboardingRepository onboarding,
            IGlobalAuthSettingsReader authSettings,
            IConfiguration config,
            IOnboardingEmailNotifier notifier,
            IClock clock,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var signingKey = LoadSigningKey(config);
            var baseUrl = config["Identity:Onboarding:ActivationBaseUrl"]
                ?? "http://localhost:4001";

            var result = await TramitesOnboardingUseCases.CreateInvitationAsync(
                new TramitesOnboardingUseCases.CreateInvitationCommand(
                    req.Email, req.InvitedRoleId, req.TenantId),
                tenant.TenantId!.Value,
                tenant.IsSuperAdmin,
                tenant.UserId!.Value,
                onboarding,
                authSettings,
                signingKey,
                baseUrl,
                notifier,
                clock,
                ct);

            return result.Match(
                ok => Results.Created(
                    $"/api/v1/identity/onboarding/invitations/{ok.InvitationId}",
                    new
                    {
                        invitationId = ok.InvitationId,
                        email = ok.Email,
                        expiresAt = ok.ExpiresAt,
                        activationUrl = ok.ActivationUrl,
                        emailQueued = ok.EmailQueued,
                    }),
                MapCreateError);
        })
        .RequireTramitesPermission(TramitesUsersEndpoints.ManageUsersPermission)
        .WithName("TramitesCreateOnboardingInvitation");

        group.MapGet("/preview", async (
            Guid invitationId,
            string token,
            string signature,
            IIdentityOnboardingRepository onboarding,
            IConfiguration config,
            IClock clock,
            CancellationToken ct) =>
        {
            var signingKey = LoadSigningKey(config);
            var result = await TramitesOnboardingUseCases.PreviewInvitationAsync(
                new TramitesOnboardingUseCases.PreviewInvitationQuery(
                    invitationId, token, signature),
                onboarding,
                signingKey,
                clock,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    valid = true,
                    email = ok.Email,
                    tenantId = ok.TenantId,
                    expiresAt = ok.ExpiresAt,
                    passwordPolicy = new
                    {
                        ok.Policy.MinLength,
                        ok.Policy.RequireUppercase,
                        ok.Policy.RequireLowercase,
                        ok.Policy.RequireDigit,
                        ok.Policy.RequireSymbol,
                    },
                }),
                MapPreviewError);
        })
        .AllowAnonymous()
        .WithName("TramitesPreviewOnboardingInvitation");

        group.MapPost("/activate", async (
            ActivateAccountRequest req,
            IIdentityOnboardingRepository onboarding,
            IPasswordHasher hasher,
            IConfiguration config,
            IClock clock,
            CancellationToken ct) =>
        {
            var signingKey = LoadSigningKey(config);
            var result = await TramitesOnboardingUseCases.ActivateAccountAsync(
                new TramitesOnboardingUseCases.ActivateAccountCommand(
                    req.InvitationId, req.Token, req.Signature, req.Password),
                onboarding,
                hasher,
                signingKey,
                clock,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    userId = ok.UserId,
                    email = ok.Email,
                    message = "Cuenta activada. Ya puede iniciar sesión.",
                }),
                code => code switch
                {
                    TramitesPasswordPolicyValidator.PolicyViolationCode =>
                        Results.BadRequest(new
                        {
                            error = code,
                            message = "La contraseña no cumple la política del tenant.",
                        }),
                    _ => MapPreviewError(code),
                });
        })
        .AllowAnonymous()
        .WithName("TramitesActivateOnboardingAccount");

        group.MapGet("/password-policy", async (
            Guid tenantId,
            IIdentityOnboardingRepository onboarding,
            CancellationToken ct) =>
        {
            await onboarding.ApplyTenantGucAsync(tenantId, ct);
            var policy = await onboarding.GetPasswordComplexityPolicyAsync(tenantId, ct);
            return Results.Ok(new
            {
                tenantId,
                policy.MinLength,
                policy.RequireUppercase,
                policy.RequireLowercase,
                policy.RequireDigit,
                policy.RequireSymbol,
                policy.HistoryCount,
                policy.MaxAgeDays,
            });
        })
        .AllowAnonymous()
        .WithName("TramitesOnboardingPasswordPolicy");

        group.MapGet("/global-settings", async (
            IGlobalAuthSettingsReader authSettings,
            CancellationToken ct) =>
        {
            var settings = await authSettings.GetAsync(ct);
            return Results.Ok(new
            {
                invitationTtlMinutes = settings.InvitationTtlMinutes,
                passwordResetTtlMinutes = settings.PasswordResetTtlMinutes,
                accessTokenTtlMinutes = settings.AccessTokenTtlMinutes,
                refreshTokenTtlDays = settings.RefreshTokenTtlDays,
            });
        })
        .AllowAnonymous()
        .WithName("TramitesOnboardingGlobalSettings");
    }

    private static byte[] LoadSigningKey(IConfiguration config)
    {
        var key = config["Identity:Onboarding:SigningKey"]
            ?? "flit-dev-onboarding-signing-key-change-in-production";
        return SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    private static bool TryEnsureAuthenticated(
        ITenantContext tenant,
        ITokenIssuer tokenIssuer,
        HttpContext http)
    {
        if (tenant.UserId.HasValue && tenant.TenantId.HasValue)
            return true;

        var access = AuthCookieWriter.ReadAccessFromRequest(http.Request);
        if (string.IsNullOrWhiteSpace(access))
            return false;

        var subject = tokenIssuer.ValidateTramitesAccess(access);
        if (subject is null)
            return false;

        tenant.Apply(
            subject.TenantId, subject.UserId, subject.IsSuperAdmin,
            trafficAgencyId: null,
            http.Items[Middleware.CorrelationIdMiddleware.ItemKey] as Guid?,
            http.Connection.RemoteIpAddress?.ToString());

        return true;
    }

    private static IResult MapCreateError(string code) => code switch
    {
        TramitesOnboardingUseCases.EmailExistsCode => Results.Conflict(new
        {
            error = code,
            message = "Ya existe una cuenta activa con ese correo.",
        }),
        TramitesOnboardingUseCases.RoleNotFoundCode => Results.BadRequest(new
        {
            error = code,
            message = "Rol invitado no encontrado.",
        }),
        TramitesOnboardingUseCases.ForbiddenTenantCode => Results.Json(
            new { error = code, message = "No puede invitar usuarios en otro tenant." },
            statusCode: StatusCodes.Status403Forbidden),
        "INVALID_EMAIL" => Results.BadRequest(new { error = code, message = "Correo inválido." }),
        _ => Results.BadRequest(new { error = code }),
    };

    private static IResult MapPreviewError(string code) => code switch
    {
        TramitesOnboardingUseCases.InvitationExpiredCode => Results.BadRequest(new
        {
            error = code,
            message = "El enlace de activación expiró.",
        }),
        TramitesOnboardingUseCases.InvitationConsumedCode => Results.Conflict(new
        {
            error = code,
            message = "El enlace ya fue utilizado.",
        }),
        _ => Results.BadRequest(new
        {
            error = code,
            message = "Enlace de activación inválido.",
        }),
    };
}
