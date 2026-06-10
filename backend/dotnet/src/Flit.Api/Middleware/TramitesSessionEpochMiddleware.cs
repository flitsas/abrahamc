using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;

namespace Flit.Api.Middleware;

/// <summary>
/// Invalida sesiones cuando permissions_epoch del JWT no coincide con BD (#9684 AC4).
/// </summary>
public sealed class TramitesSessionEpochMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SkipPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/health",
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/auth/password-reset",
        "/api/v1/identity/onboarding",
    };

    public async Task InvokeAsync(
        HttpContext http,
        ITenantContext tenant,
        ITokenIssuer tokenIssuer,
        IIdentityAccountRepository accounts)
    {
        var path = http.Request.Path.Value ?? string.Empty;
        if (ShouldSkip(path))
        {
            await next(http).ConfigureAwait(false);
            return;
        }

        var access = AuthCookieWriter.ReadAccessFromRequest(http.Request);
        if (!string.IsNullOrWhiteSpace(access))
        {
            var subject = tokenIssuer.ValidateTramitesAccess(access);
            if (subject is not null)
            {
                var currentEpoch = await accounts.GetPermissionsEpochAsync(
                    subject.UserId, http.RequestAborted);

                if (currentEpoch != subject.PermissionsEpoch)
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new
                    {
                        error = TramitesAuthUseCases.PermissionsStaleCode,
                        message = "Sus permisos cambiaron. Inicie sesión nuevamente.",
                    }, http.RequestAborted);
                    return;
                }
            }
        }

        await next(http).ConfigureAwait(false);
    }

    private static bool ShouldSkip(string path)
    {
        foreach (var prefix in SkipPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
