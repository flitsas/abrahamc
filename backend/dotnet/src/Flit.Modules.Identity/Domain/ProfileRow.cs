namespace Flit.Modules.Identity.Domain;

/// <summary>Perfil autoservicio del colaborador (RF-4.3).</summary>
public sealed record ProfileRow(
    Guid UserId,
    Guid TenantId,
    string Email,
    string? FullName,
    string? Phone,
    string? Address,
    string Locale,
    string Timezone,
    string AccountState,
    IReadOnlyList<string> RoleSlugs,
    int RowVersion);
