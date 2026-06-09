namespace Flit.Modules.Identity.Domain;

/// <summary>Compañía B2B (identity.tenants, HU #9419).</summary>
public sealed record TenantRow(
    Guid Id,
    string Name,
    string Nit,
    string Slug,
    string Status,
    string SettingsJson,
    int RowVersion);
