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

public sealed record TramitesSessionSubject(
    Guid UserId,
    Guid TenantId,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyList<string> PermissionSlugs);

public sealed record TramitesLoginResponse(
    Guid UserId,
    string Email,
    Guid TenantId,
    string AccountState,
    bool IsSuperAdmin,
    IReadOnlyList<string> PermissionSlugs,
    int ExpiresInSeconds);
