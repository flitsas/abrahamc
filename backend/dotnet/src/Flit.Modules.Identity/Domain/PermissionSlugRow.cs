namespace Flit.Modules.Identity.Domain;

/// <summary>Permiso global por slug (identity.permissions, HU #9417).</summary>
public sealed record PermissionSlugRow(
    Guid Id,
    string Slug,
    string Module,
    string Action,
    string? Description,
    bool IsActive,
    bool IsAssignable,
    bool IsSystem);
