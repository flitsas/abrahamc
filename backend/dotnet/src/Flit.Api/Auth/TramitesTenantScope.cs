using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Identity.Application;

namespace Flit.Api.Auth;

/// <summary>
/// Resuelve tenant efectivo desde JWT/cookies + override opcional (query/body/header vía session).
/// Alinea procedures-config y procedures con el patrón de Compañías B2B (#9445).
/// </summary>
public static class TramitesTenantScope
{
    public static bool TryResolve(
        ITenantContext tenantContext,
        ICompaniesSessionContext session,
        Guid? requestTenantId,
        out Guid effectiveTenantId,
        out IResult? errorResult)
    {
        effectiveTenantId = Guid.Empty;
        errorResult = null;

        if (!tenantContext.UserId.HasValue)
        {
            errorResult = Results.Unauthorized();
            return false;
        }

        Guid? explicitTenant = requestTenantId is { } id && id != Guid.Empty ? id : null;
        var resolved = TramitesAdminContext.ResolveEffectiveTenant(
            tenantContext.TenantId,
            tenantContext.IsSuperAdmin,
            explicitTenant ?? session.TenantId);

        if (!resolved.IsSuccess)
        {
            errorResult = MapError(resolved.Error);
            return false;
        }

        effectiveTenantId = resolved.Value;
        return true;
    }

    public static Guid ResolveActorUserId(ICompaniesSessionContext session) =>
        session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");

    private static IResult MapError(string code) => code switch
    {
        TramitesAdminContext.TenantMismatchCode => Results.Json(
            new
            {
                error = code,
                message = "El tenant del recurso no coincide con el contexto de la sesión.",
            },
            statusCode: StatusCodes.Status403Forbidden),
        TramitesAdminContext.TenantContextRequiredCode => Results.BadRequest(new
        {
            error = code,
            message = "Contexto de tenant requerido.",
        }),
        _ => Results.Problem(code),
    };
}
