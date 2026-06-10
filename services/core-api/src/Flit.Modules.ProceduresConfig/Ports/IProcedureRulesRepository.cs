using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public interface IProcedureRulesRepository
{
    /// <summary>Reglas vivas: activas, no borradas lógicamente, ordenadas por prioridad ascendente.</summary>
    Task<IReadOnlyList<ProcedureRuleRecord>> ListActiveForEvaluationAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct = default);
}
