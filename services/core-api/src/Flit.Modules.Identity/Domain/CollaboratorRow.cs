namespace Flit.Modules.Identity.Domain;

/// <summary>Usuario colaborador con perfil y roles (HU #9419).</summary>
public sealed record CollaboratorRow(
    Guid Id,
    Guid TenantId,
    string Email,
    string AccountState,
    string? FullName,
    string? Phone,
    IReadOnlyList<string> RoleSlugs,
    int RowVersion);
