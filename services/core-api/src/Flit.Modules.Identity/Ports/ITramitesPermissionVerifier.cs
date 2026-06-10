using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Verificador runtime de slugs — consulta BD activa (HU #9417, AC2).</summary>
public interface ITramitesPermissionVerifier
{
    Task<bool> HasSlugAsync(
        Guid userId,
        Guid tenantId,
        bool isSuperAdmin,
        string requiredSlug,
        CancellationToken ct = default);

    Task<bool> HasSlugWithAbacAsync(
        Guid userId,
        Guid tenantId,
        bool isSuperAdmin,
        string requiredSlug,
        AbacEvaluationContext? abacContext,
        CancellationToken ct = default);
}
