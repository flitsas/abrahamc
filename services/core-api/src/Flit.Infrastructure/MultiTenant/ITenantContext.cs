namespace Flit.Infrastructure.MultiTenant;

/// <summary>
/// Contexto de sesión por petición HTTP (HU #9414 — GUC PostgreSQL / RLS).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    Guid? TrafficAgencyId { get; }
    bool IsSuperAdmin { get; }
    Guid? RequestId { get; }
    string? ClientIp { get; }

    /// <summary>Indica si hay al menos tenant o usuario para aplicar GUCs.</summary>
    bool IsConfigured { get; }

    void Apply(
        Guid? tenantId,
        Guid? userId,
        bool isSuperAdmin,
        Guid? trafficAgencyId = null,
        Guid? requestId = null,
        string? clientIp = null);
}
