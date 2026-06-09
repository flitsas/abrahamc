namespace Flit.Modules.Companies.Ports;

/// <summary>Contexto de sesión B2B (tenant + super admin) para RLS y filtros de indexación.</summary>
public interface ICompaniesSessionContext
{
    Guid? TenantId { get; }
    bool IsSuperAdmin { get; }
    Guid? ActorUserId { get; }
}
