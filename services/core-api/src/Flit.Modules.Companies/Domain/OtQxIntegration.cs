namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Configuración de integración QX por Organismo de Tránsito (ot.ot_qx_integrations — HU #9455 OT-02).
/// Determina si el OT opera en modo Dashboard (FLIT puro) o modo QX (cola externa Quipux).
/// La tabla tiene RLS por app.current_agency_id (ADR-0012: sin tenant_id).
/// </summary>
public sealed class OtQxIntegration
{
    public static class Modes
    {
        public const string Dashboard = "dashboard";
        public const string Qx = "qx";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Dashboard, Qx };
    }

    public Guid Id { get; private set; }
    public Guid TrafficAgencyId { get; private set; }
    public string Mode { get; private set; } = Modes.Dashboard;
    public string? CallbackUrl { get; private set; }
    public string AuthConfigJson { get; private set; } = "{}";
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public int RowVersion { get; private set; }

    private OtQxIntegration() { }

    public bool IsDashboardMode => string.Equals(Mode, Modes.Dashboard, StringComparison.OrdinalIgnoreCase);
    public bool IsQxMode => string.Equals(Mode, Modes.Qx, StringComparison.OrdinalIgnoreCase);
}
