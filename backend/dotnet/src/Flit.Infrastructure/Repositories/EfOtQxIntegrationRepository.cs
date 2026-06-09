using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF Core para ot.ot_qx_integrations (HU #9455 OT-02).
/// La tabla usa RLS por app.current_agency_id (ADR-0012).
/// </summary>
public sealed class EfOtQxIntegrationRepository(FlitDbContext db) : IOtQxIntegrationRepository
{
    public async Task<OtQxIntegration?> GetByTrafficAgencyAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        return await db.OtQxIntegrations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TrafficAgencyId == trafficAgencyId && x.IsActive, ct);
    }
}
