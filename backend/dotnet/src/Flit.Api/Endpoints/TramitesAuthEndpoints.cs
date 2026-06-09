using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;
using IClock = Flit.SharedKernel.IClock;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;

namespace Flit.Api.Endpoints;

/// <summary>Auth Trámites 2.0 — login local (#9415).</summary>
public static class TramitesAuthEndpoints
{
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string? RefreshToken);
    public sealed record LogoutRequest(string? RefreshToken);

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
                        user = ok.Session,
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
                        user = ok.Session,
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
                permissionSlugs,
            });
        })
        .WithName("TramitesAuthMe");
    }
}
