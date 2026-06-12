namespace Flit.Modules.Procedures.Domain;

/// <summary>
/// Puerto del repositorio de instancias de trámite (TRA-02 #9434).
/// La capa de aplicación depende de esta interfaz; la infraestructura la implementa.
/// </summary>
public interface IProcedureInstanceRepository
{
    Task<ProcedureInstance?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcedureInstance>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<ProcedureInstanceSearchResult> SearchAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? state,
        string? procedureTypeCode,
        CancellationToken ct = default);

    Task<int> GetNextSequenceAsync(
        Guid tenantId,
        Guid procedureTypeId,
        Guid? trafficAgencyId,
        CancellationToken ct = default);

    Task<ProcedureReferenceContext> ResolveReferenceContextAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        string procedureTypeCode,
        CancellationToken ct = default);

    Task SaveAsync(ProcedureInstance instance, CancellationToken ct = default);
}
