namespace Flit.Modules.Identity.Domain;

/// <summary>
/// Roles RBAC del MVP (ADR-0006 §"Resumen ejecutivo de A").
/// Post-MVP: ABAC con conditions JSONB en identity_role_permission.
/// </summary>
public enum Rol
{
    Ciudadano,
    Funcionario,
    Admin,
}

public static class RolExtensions
{
    public static string ToClaimValue(this Rol rol) => rol switch
    {
        Rol.Ciudadano => "ciudadano",
        Rol.Funcionario => "funcionario",
        Rol.Admin => "admin",
        _ => "ciudadano",
    };

    public static Rol FromClaim(string? claim) => claim?.ToLowerInvariant() switch
    {
        "funcionario" => Rol.Funcionario,
        "admin" => Rol.Admin,
        _ => Rol.Ciudadano,
    };
}
