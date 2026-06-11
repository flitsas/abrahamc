namespace Flit.Modules.Companies.Ports;

/// <summary>Aprovisiona identity.tenants para el maestro B2B (#9687).</summary>
public interface ICompanyTenantProvisioner
{
    Task<bool> NitExistsAsync(string nit, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    Task<ProvisionedCompanyTenant> CreateAsync(
        string name,
        string nit,
        string slug,
        string? settingsJson,
        Guid createdBy,
        CancellationToken ct = default);
}

public sealed record ProvisionedCompanyTenant(
    Guid Id,
    string Name,
    string Nit,
    string Slug,
    string Status);
