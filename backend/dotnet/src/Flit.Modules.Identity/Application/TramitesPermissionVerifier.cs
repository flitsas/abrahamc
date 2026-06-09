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
        CancellationToken ct = default)
    {
        if (isSuperAdmin)
            return Task.FromResult(true);

        return rbac.UserHasPermissionSlugAsync(userId, tenantId, requiredSlug.Trim(), ct);
    }
}
