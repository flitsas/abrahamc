namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>
/// Guard de transiciones de estado de instancias (#9432 / runtime).
/// Alineado con <c>procedures.validate_state_transition()</c> (TRA-01 #9433).
/// Máquina de 9 estados: borrador → asignado → q_validacion → pendiente → aprobado → enviado → entregado | anulado.
/// </summary>
public static class ProcedureStateTransitionGuard
{
    private static HashSet<string> S(params string[] states) =>
        new(states, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, HashSet<string>> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["borrador"] = S("asignado", "q_validacion", "anulado"),
        ["asignado"] = S("q_validacion", "borrador", "anulado"),
        ["q_validacion"] = S("pendiente", "rechazado", "borrador", "anulado"),
        ["pendiente"] = S("aprobado", "rechazado", "anulado"),
        ["aprobado"] = S("enviado", "anulado"),
        ["enviado"] = S("entregado", "rechazado"),
        ["rechazado"] = S("borrador", "anulado"),
        ["entregado"] = S(),
        ["anulado"] = S(),
    };

    public static bool CanTransition(string fromState, string toState) =>
        Allowed.TryGetValue(fromState, out var targets) &&
        targets.Contains(toState);

    public static IReadOnlyList<string> GetAllowedTargets(string fromState) =>
        Allowed.TryGetValue(fromState, out var targets) ? [.. targets] : [];
}
