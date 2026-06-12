using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

/// <summary>Puerto de lectura para ot.ot_qx_integrations (HU #9455 OT-02).</summary>
public interface IOtQxIntegrationRepository
{
    /// <summary>
    /// Obtiene la configuración QX para un OT dado.
    /// Retorna null si el OT no tiene integración QX configurada (se asume modo dashboard por defecto).
    /// </summary>
    Task<OtQxIntegration?> GetByTrafficAgencyAsync(Guid trafficAgencyId, CancellationToken ct = default);

    Task<OtQxIntegration> UpsertModeAsync(
        Guid trafficAgencyId,
        string mode,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
