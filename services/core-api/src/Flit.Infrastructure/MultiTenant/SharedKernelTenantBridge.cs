using Flit.SharedKernel;

namespace Flit.Infrastructure.MultiTenant;

/// <summary>
/// Adapta <see cref="ITenantContext"/> de Identidad (#9414) al contrato legacy de SharedKernel
/// usado por IDSecure (#9469) vía <c>SetTenant</c>.
/// </summary>
public sealed class SharedKernelTenantBridge(ITenantContext inner) : Flit.SharedKernel.ITenantContext
{
    public Guid? TenantId => inner.TenantId;

    public void SetTenant(Guid tenantId) =>
        inner.Apply(
            tenantId,
            inner.UserId,
            inner.IsSuperAdmin,
            inner.TrafficAgencyId,
            inner.RequestId,
            inner.ClientIp);
}
