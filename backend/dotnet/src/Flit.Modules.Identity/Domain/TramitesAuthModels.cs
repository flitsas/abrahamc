namespace Flit.Modules.Identity.Domain;

/// <summary>Registro mínimo devuelto por identity.find_user_for_auth (HU #9415).</summary>
public sealed record AuthUserRow(
    Guid Id,
    Guid TenantId,
    string Email,
    string? PasswordHash,
    string PasswordAlgo,
    string AccountState,
    DateTimeOffset? BlockedUntil);

/// <summary>Roles y permisos efectivos (unión aditiva multi-rol) + epoch de sesión (#9683).</summary>
public sealed record UserAuthContext(
    IReadOnlyList<string> RoleSlugs,
    IReadOnlyList<string> PermissionSlugs,
    int PermissionsEpoch);

public sealed record TramitesSessionSubject(
    Guid UserId,
    Guid TenantId,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyList<string> RoleSlugs,
    IReadOnlyList<string> PermissionSlugs,
    int PermissionsEpoch);

public sealed record TramitesLoginResponse(
    Guid UserId,
    string Email,
    Guid TenantId,
    string AccountState,
    bool IsSuperAdmin,
    IReadOnlyList<string> RoleSlugs,
    IReadOnlyList<string> PermissionSlugs,
    int ExpiresInSeconds);
