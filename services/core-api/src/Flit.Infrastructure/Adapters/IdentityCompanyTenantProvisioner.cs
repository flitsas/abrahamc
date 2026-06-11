using Flit.Modules.Companies.Ports;
using Flit.Modules.Identity.Ports;

namespace Flit.Infrastructure.Adapters;

public sealed class IdentityCompanyTenantProvisioner(IIdentityAdminRepository admin)
    : ICompanyTenantProvisioner
{
    public Task<bool> NitExistsAsync(string nit, CancellationToken ct = default) =>
        admin.TenantNitExistsAsync(nit.Trim(), excludeId: null, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        admin.TenantSlugExistsAsync(slug.Trim().ToLowerInvariant(), excludeId: null, ct);

    public async Task<ProvisionedCompanyTenant> CreateAsync(
        string name,
        string nit,
        string slug,
        string? settingsJson,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var row = await admin.CreateTenantAsync(
            name.Trim(),
            nit.Trim(),
            slug.Trim().ToLowerInvariant(),
            settingsJson,
            createdBy,
            ct);

        return new ProvisionedCompanyTenant(
            row.Id,
            row.Name,
            row.Nit,
            row.Slug,
            row.Status);
    }
}
