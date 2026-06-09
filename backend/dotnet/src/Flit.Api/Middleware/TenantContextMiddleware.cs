using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Ports;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Flit.Api.Middleware;

/// <summary>
/// Resuelve contexto multi-tenant desde JWT (sin validar firma aquí; #9415 validará en login)
/// o cabeceras de desarrollo. HU #9414.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public const string TenantHeader = "X-Tenant-Id";
    public const string UserHeader = "X-User-Id";
    public const string SuperAdminHeader = "X-Is-Super-Admin";
    public const string AgencyHeader = "X-Traffic-Agency-Id";

    private static readonly JsonWebTokenHandler JwtReader = new();

    public async Task InvokeAsync(HttpContext http, ITenantContext tenantContext, ITokenIssuer tokenIssuer)
    {
        var requestId = http.Items[CorrelationIdMiddleware.ItemKey] as Guid?;
        var clientIp = http.Connection.RemoteIpAddress?.ToString();

        if (TryResolveFromValidatedAccessToken(http, tokenIssuer, out var subject))
        {
            tenantContext.Apply(
                subject.TenantId, subject.UserId, subject.IsSuperAdmin,
                trafficAgencyId: null, requestId, clientIp);
            await next(http).ConfigureAwait(false);
            return;
        }

        if (TryResolveFromJwt(http, out var jwtTenant, out var jwtUser, out var jwtSuperAdmin, out var jwtAgency))
        {
            tenantContext.Apply(jwtTenant, jwtUser, jwtSuperAdmin, jwtAgency, requestId, clientIp);
            await next(http).ConfigureAwait(false);
            return;
        }

        if (TryResolveFromDevHeaders(http, out var hdrTenant, out var hdrUser, out var hdrSuperAdmin, out var hdrAgency))
        {
            tenantContext.Apply(hdrTenant, hdrUser, hdrSuperAdmin, hdrAgency, requestId, clientIp);
            await next(http).ConfigureAwait(false);
            return;
        }

        tenantContext.Apply(null, null, isSuperAdmin: false, trafficAgencyId: null, requestId, clientIp);
        await next(http).ConfigureAwait(false);
    }

    private static bool TryResolveFromValidatedAccessToken(
        HttpContext http,
        ITokenIssuer tokenIssuer,
        out Flit.Modules.Identity.Domain.TramitesSessionSubject subject)
    {
        subject = null!;
        var auth = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var cookie = http.Request.Cookies[Flit.Api.Auth.AuthCookieNames.Access];
            if (string.IsNullOrWhiteSpace(cookie))
                return false;
            auth = "Bearer " + cookie;
        }

        var token = auth["Bearer ".Length..].Trim();
        var validated = tokenIssuer.ValidateTramitesAccess(token);
        if (validated is null)
            return false;

        subject = validated;
        return true;
    }

    private static bool TryResolveFromJwt(
        HttpContext http,
        out Guid? tenantId,
        out Guid? userId,
        out bool isSuperAdmin,
        out Guid? agencyId)
    {
        tenantId = null;
        userId = null;
        isSuperAdmin = false;
        agencyId = null;

        var auth = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var token = auth["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return false;

        JsonWebToken jwt;
        try
        {
            jwt = JwtReader.ReadJsonWebToken(token);
        }
        catch (Exception)
        {
            return false;
        }

        if (Guid.TryParse(jwt.Subject, out var sub))
            userId = sub;

        var tenantClaim = jwt.GetClaim("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tid))
            tenantId = tid;

        var superClaim = jwt.GetClaim("is_super_admin")?.Value;
        isSuperAdmin = string.Equals(superClaim, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(superClaim, "1", StringComparison.OrdinalIgnoreCase);

        var agencyClaim = jwt.GetClaim("current_agency_id")?.Value
            ?? jwt.GetClaim("agency_id")?.Value;
        if (Guid.TryParse(agencyClaim, out var aid))
            agencyId = aid;

        return tenantId.HasValue || userId.HasValue || isSuperAdmin;
    }

    private static bool TryResolveFromDevHeaders(
        HttpContext http,
        out Guid? tenantId,
        out Guid? userId,
        out bool isSuperAdmin,
        out Guid? agencyId)
    {
        tenantId = ParseGuidHeader(http, TenantHeader);
        userId = ParseGuidHeader(http, UserHeader);
        agencyId = ParseGuidHeader(http, AgencyHeader);

        var superRaw = http.Request.Headers[SuperAdminHeader].FirstOrDefault();
        isSuperAdmin = string.Equals(superRaw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(superRaw, "1", StringComparison.OrdinalIgnoreCase);

        if (isSuperAdmin && tenantId.HasValue)
            return true;

        return tenantId.HasValue || userId.HasValue || isSuperAdmin;
    }

    private static Guid? ParseGuidHeader(HttpContext http, string name)
    {
        var raw = http.Request.Headers[name].FirstOrDefault();
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
