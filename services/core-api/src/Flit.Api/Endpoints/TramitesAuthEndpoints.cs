using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;
using Flit.Modules.Notifications.Domain;
using Flit.Modules.Notifications.Ports;
using IClock = Flit.SharedKernel.IClock;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;

namespace Flit.Api.Endpoints;

/// <summary>Auth Trámites 2.0 — login local (#9415).</summary>
public static class TramitesAuthEndpoints
{
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string? RefreshToken);
    public sealed record LogoutRequest(string? RefreshToken);
    public sealed record PasswordResetRequestCommand(string Email);
    public sealed record PasswordResetConfirmCommand(string ResetToken, string NewPassword);

    public static void MapTramitesAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        group.MapPost("/login", async (
            LoginRequest req,
            HttpContext http,
            IIdentityAccountRepository accounts,
            IPasswordHasher hasher,
            ITokenIssuer tokenIssuer,
            IClock clock,
            IHostEnvironment env,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var refreshDays = int.TryParse(config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
            var result = await TramitesAuthUseCases.LoginAsync(
                new TramitesAuthUseCases.LoginCommand(
                    req.Email, req.Password,
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString()),
                accounts, hasher, tokenIssuer, clock,
                TimeSpan.FromDays(refreshDays), ct);

            return result.Match(
                ok =>
                {
                    AuthCookieWriter.SetSessionCookies(
                        http.Response, env,
                        ok.AccessToken, ok.AccessExpiresAt,
                        ok.RefreshToken, ok.RefreshExpiresAt);
                    return Results.Ok(new
                    {
                        accessToken = ok.AccessToken,
                        refreshToken = ok.RefreshToken,
                        user = ok.Session,
                        roleSlugs = ok.Session.RoleSlugs,
                        permissionSlugs = ok.Session.PermissionSlugs,
                        expiresIn = ok.Session.ExpiresInSeconds,
                    });
                },
                err => Results.Json(
                    new
                    {
                        error = err.Code,
                        message = err.Message,
                        retryAfterSeconds = err.RetryAfterSeconds,
                    },
                    statusCode: err.Kind == TramitesAuthUseCases.LoginFailureKind.TemporarilyLocked
                        ? StatusCodes.Status423Locked
                        : StatusCodes.Status401Unauthorized));
        })
        .WithName("TramitesLogin")
        .AllowAnonymous();

        group.MapPost("/refresh", async (
            RefreshRequest req,
            HttpContext http,
            IIdentityAccountRepository accounts,
            ITokenIssuer tokenIssuer,
            IClock clock,
            IHostEnvironment env,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var refreshToken = req.RefreshToken
                ?? http.Request.Cookies[AuthCookieNames.Refresh];
            if (string.IsNullOrWhiteSpace(refreshToken))
                return Results.Json(
                    new { error = TramitesAuthUseCases.InvalidCredentialsCode, message = "Credenciales inválidas." },
                    statusCode: StatusCodes.Status401Unauthorized);

            var refreshDays = int.TryParse(config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
            var result = await TramitesAuthUseCases.RefreshAsync(
                new TramitesAuthUseCases.RefreshCommand(
                    refreshToken,
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString()),
                accounts, tokenIssuer, clock,
                TimeSpan.FromDays(refreshDays), ct);

            return result.Match(
                ok =>
                {
                    AuthCookieWriter.SetSessionCookies(
                        http.Response, env,
                        ok.AccessToken, ok.AccessExpiresAt,
                        ok.RefreshToken, ok.RefreshExpiresAt);
                    return Results.Ok(new
                    {
                        accessToken = ok.AccessToken,
                        refreshToken = ok.RefreshToken,
                        user = ok.Session,
                        roleSlugs = ok.Session.RoleSlugs,
                        permissionSlugs = ok.Session.PermissionSlugs,
                        expiresIn = ok.Session.ExpiresInSeconds,
                    });
                },
                err => Results.Json(
                    new { error = err.Code, message = err.Message },
                    statusCode: StatusCodes.Status401Unauthorized));
        })
        .WithName("TramitesRefresh")
        .AllowAnonymous();

        group.MapPost("/logout", async (
            LogoutRequest req,
            HttpContext http,
            IIdentityAccountRepository accounts,
            IClock clock,
            CancellationToken ct) =>
        {
            var refreshToken = req.RefreshToken
                ?? http.Request.Cookies[AuthCookieNames.Refresh];
            await TramitesAuthUseCases.LogoutAsync(refreshToken, accounts, clock, ct);
            AuthCookieWriter.ClearSessionCookies(http.Response);
            return Results.NoContent();
        })
        .WithName("TramitesLogout");

        group.MapPost("/password-reset/request", async (
            PasswordResetRequestCommand req,
            HttpContext http,
            IIdentityAccountRepository accounts,
            IIdentityPasswordResetRepository resets,
            IGlobalAuthSettingsReader authSettings,
            IGlobalEmailService emailService,
            IConfiguration config,
            IClock clock,
            CancellationToken ct) =>
        {
            var issued = await TramitesPasswordResetUseCases.RequestAsync(
                new TramitesPasswordResetUseCases.RequestCommand(
                    req.Email,
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString()),
                accounts,
                resets,
                authSettings,
                clock,
                ct);

            if (issued is not null && emailService.IsConfigured)
            {
                var baseUrl = config["Identity:Onboarding:ActivationBaseUrl"] ?? "http://localhost:5173";
                var resetUrl = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(issued.RawToken)}";
                await emailService.SendTemplatedAsync(
                    issued.RecipientEmail,
                    EmailTemplateKeys.PasswordReset,
                    new Dictionary<string, string>
                    {
                        ["reset_url"] = resetUrl,
                        ["expires_at"] = issued.ExpiresAt.ToString("f"),
                    },
                    ct: ct);
            }

            return Results.Ok(new
            {
                message = "Si el correo está registrado, recibirá instrucciones para restablecer la contraseña.",
            });
        })
        .AllowAnonymous()
        .WithName("TramitesPasswordResetRequest");

        group.MapPost("/password-reset/confirm", async (
            PasswordResetConfirmCommand req,
            IIdentityAccountRepository accounts,
            IIdentityOnboardingRepository onboarding,
            IIdentityPasswordResetRepository resets,
            IPasswordHasher hasher,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await TramitesPasswordResetUseCases.ConfirmAsync(
                new TramitesPasswordResetUseCases.ConfirmCommand(req.ResetToken, req.NewPassword),
                accounts,
                onboarding,
                resets,
                hasher,
                clock,
                ct);

            return result.Match(
                _ => Results.Ok(new { message = "Contraseña actualizada. Ya puede iniciar sesión." }),
                code => code switch
                {
                    TramitesPasswordResetUseCases.PolicyViolationCode => Results.BadRequest(new
                    {
                        error = code,
                        message = "La contraseña no cumple la política del tenant.",
                    }),
                    _ => Results.BadRequest(new
                    {
                        error = code,
                        message = "Token de restablecimiento inválido o expirado.",
                    }),
                });
        })
        .AllowAnonymous()
        .WithName("TramitesPasswordResetConfirm");

        group.MapGet("/me", async (
            HttpContext http,
            ITokenIssuer tokenIssuer,
            ITenantContext tenantContext,
            IIdentityRbacRepository rbac,
            CancellationToken ct) =>
        {
            var access = AuthCookieWriter.ReadAccessFromRequest(http.Request);
            if (string.IsNullOrWhiteSpace(access))
                return Results.Unauthorized();

            var subject = tokenIssuer.ValidateTramitesAccess(access);
            if (subject is null)
                return Results.Unauthorized();

            tenantContext.Apply(
                subject.TenantId, subject.UserId, subject.IsSuperAdmin,
                trafficAgencyId: null,
                http.Items[Middleware.CorrelationIdMiddleware.ItemKey] as Guid?,
                http.Connection.RemoteIpAddress?.ToString());

            var permissionSlugs = await rbac.GetEffectiveSlugsAsync(
                subject.UserId, subject.TenantId, ct);

            return Results.Ok(new
            {
                user = new
                {
                    id = subject.UserId,
                    email = subject.Email,
                    tenantId = subject.TenantId,
                    isSuperAdmin = subject.IsSuperAdmin,
                },
                roleSlugs = subject.RoleSlugs,
                permissionSlugs,
                permissionsEpoch = subject.PermissionsEpoch,
            });
        })
        .WithName("TramitesAuthMe");
    }
}
