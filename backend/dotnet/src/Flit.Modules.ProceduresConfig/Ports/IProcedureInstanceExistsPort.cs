namespace Flit.Modules.ProceduresConfig.Ports;

/// <summary>
/// Comprueba existencia de instancia sin acoplar ProceduresConfig al módulo Procedures (evita ciclo de refs).
/// </summary>
public interface IProcedureInstanceExistsPort
{
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
