using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Companies.Ports;

namespace Flit.Api.Services;

/// <summary>
/// Resuelve contexto B2B desde JWT/cookies (ITenantContext) con override de tenant
/// acotado vía <c>X-Flit-Tenant-Id</c> al operar configs de una compañía concreta (SA/TA).
/// Fallback a headers legacy solo sin sesión JWT (DEV local).
/// </summary>
public sealed class HttpCompaniesSessionContext(
    IHttpContextAccessor http,
    ITenantContext tenantContext) : ICompaniesSessionContext
{
    private const string ScopedTenantHeader = "X-Flit-Tenant-Id";
    private const string LegacySuperAdminHeader = "X-Flit-Super-Admin";
    private const string LegacyUserHeader = "X-Flit-User-Id";

    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid TramitesSuperAdminId = Guid.Parse("01930201-0001-7001-8001-000000000010");
    private static readonly Guid LegacyShellDevUserId = Guid.Parse("01900000-100b-7001-8001-000000000001");

    public bool IsSuperAdmin =>
        HasJwtContext
            ? tenantContext.IsSuperAdmin
            : IsLegacySuperAdminHeader();

    public Guid? ActorUserId
    {
        get
        {
            if (tenantContext.UserId is Guid jwtUser)
            {
                return jwtUser;
            }

            var legacy = TryParseGuid(http.HttpContext?.Request.Headers[LegacyUserHeader].FirstOrDefault());
            if (legacy is null)
            {
                return SystemUserId;
            }

            return legacy == LegacyShellDevUserId ? TramitesSuperAdminId : legacy;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var scoped = TryParseGuid(http.HttpContext?.Request.Headers[ScopedTenantHeader].FirstOrDefault());

            if (HasJwtContext)
            {
                if (tenantContext.IsSuperAdmin)
                {
                    return scoped ?? tenantContext.TenantId;
                }

                if (tenantContext.TenantId is Guid jwtTenant)
                {
                    if (scoped.HasValue && scoped != jwtTenant)
                    {
                        return jwtTenant;
                    }

                    return scoped ?? jwtTenant;
                }

                return scoped;
            }

            return scoped;
        }
    }

    private bool HasJwtContext => tenantContext.UserId.HasValue;

    private bool IsLegacySuperAdminHeader() =>
        string.Equals(
            http.HttpContext?.Request.Headers[LegacySuperAdminHeader].FirstOrDefault(),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static Guid? TryParseGuid(string? value)
        => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
}
