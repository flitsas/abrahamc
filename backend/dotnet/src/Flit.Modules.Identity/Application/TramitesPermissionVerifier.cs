using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;

namespace Flit.Modules.Identity.Application;

/// <summary>Verificación de permisos desde identity.* sin caché de larga duración (AC1/AC2).</summary>
public sealed class TramitesPermissionVerifier(IIdentityRbacRepository rbac) : ITramitesPermissionVerifier
{
    public Task<bool> HasSlugAsync(
        Guid userId,
        Guid tenantId,
        bool isSuperAdmin,
        string requiredSlug,
        CancellationToken ct = default) =>
        HasSlugWithAbacAsync(userId, tenantId, isSuperAdmin, requiredSlug, abacContext: null, ct);

    public async Task<bool> HasSlugWithAbacAsync(
        Guid userId,
        Guid tenantId,
        bool isSuperAdmin,
        string requiredSlug,
        AbacEvaluationContext? abacContext,
        CancellationToken ct = default)
    {
        if (isSuperAdmin)
            return true;

        var slug = requiredSlug.Trim();
        if (!await rbac.UserHasPermissionSlugAsync(userId, tenantId, slug, ct))
            return false;

        if (abacContext is null)
            return true;

        var conditions = await rbac.GetAbacConditionsForSlugAsync(userId, tenantId, slug, ct);
        if (conditions.All(string.IsNullOrWhiteSpace))
            return true;

        return TramitesAbacEvaluator.IsGranted(conditions, abacContext);
    }
}
