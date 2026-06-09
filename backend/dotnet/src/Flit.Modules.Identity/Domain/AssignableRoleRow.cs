namespace Flit.Modules.Identity.Domain;

/// <summary>Rol global asignable a colaboradores (identity.roles).</summary>
public sealed record AssignableRoleRow(
    Guid Id,
    string Slug,
    string Name,
    string Scope);
