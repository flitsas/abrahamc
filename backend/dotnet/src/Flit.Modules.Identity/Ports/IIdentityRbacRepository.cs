using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>RBAC Trámites 2.0 — slugs en identity.* (HU #9417).</summary>
public interface IIdentityRbacRepository
{
    Task<bool> UserHasPermissionSlugAsync(
        Guid userId, Guid tenantId, string slug, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetEffectiveSlugsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<PermissionSlugRow>> ListPermissionsAsync(CancellationToken ct = default);

    Task<PermissionSlugRow?> GetPermissionByIdAsync(Guid id, CancellationToken ct = default);

    Task<PermissionSlugRow?> GetPermissionBySlugAsync(string slug, CancellationToken ct = default);

    Task<PermissionSlugRow> CreatePermissionAsync(
        string slug,
        string moduleName,
        string action,
        string? description,
        Guid? createdBy,
        CancellationToken ct = default);

    Task<bool> SetPermissionActiveAsync(
        Guid id, bool isActive, CancellationToken ct = default);
}
