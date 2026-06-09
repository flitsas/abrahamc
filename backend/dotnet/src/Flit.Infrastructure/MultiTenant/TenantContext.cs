namespace Flit.Infrastructure.MultiTenant;

/// <summary>Implementación scoped de <see cref="ITenantContext"/>.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? TrafficAgencyId { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public Guid? RequestId { get; private set; }
    public string? ClientIp { get; private set; }

    public bool IsConfigured => TenantId.HasValue || UserId.HasValue || IsSuperAdmin;

    public void Apply(
        Guid? tenantId,
        Guid? userId,
        bool isSuperAdmin,
        Guid? trafficAgencyId = null,
        Guid? requestId = null,
        string? clientIp = null)
    {
        TenantId = tenantId;
        UserId = userId;
        IsSuperAdmin = isSuperAdmin;
        TrafficAgencyId = trafficAgencyId;
        RequestId = requestId;
        ClientIp = clientIp;
    }
}
