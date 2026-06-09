namespace Flit.Infrastructure.MultiTenant;

/// <summary>Nombres de variables de sesión PostgreSQL (RLS / audit).</summary>
public static class TenantGucNames
{
    public const string CurrentTenantId = "app.current_tenant_id";
    public const string CurrentUserId = "app.current_user_id";
    public const string IsSuperAdmin = "app.is_super_admin";
    public const string CurrentAgencyId = "app.current_agency_id";
    public const string RequestId = "app.request_id";
    public const string ClientIp = "app.client_ip";
}
