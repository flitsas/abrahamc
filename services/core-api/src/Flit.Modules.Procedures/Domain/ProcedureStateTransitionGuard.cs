namespace Flit.Modules.Procedures.Domain;

/// <summary>
/// Guard de la máquina de estados de instancias de trámite (#9408 FR-5).
/// Espejo exacto del trigger <c>procedures.validate_state_transition()</c> en BD.
///
/// <code>
/// borrador     → asignado | q_validacion | anulado
/// asignado     → q_validacion | borrador | anulado
/// q_validacion → pendiente | rechazado | borrador | anulado
/// pendiente    → aprobado | rechazado | anulado
/// aprobado     → enviado | anulado
/// enviado      → entregado | rechazado
/// rechazado    → borrador | anulado
/// entregado    → (terminal)
/// anulado      → (terminal)
/// </code>
/// </summary>
public static class ProcedureStateTransitionGuard
{
    private static HashSet<string> S(params string[] states) =>
        new(states, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, HashSet<string>> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ProcedureStates.Borrador] = S(ProcedureStates.Asignado, ProcedureStates.QValidacion, ProcedureStates.Anulado),
            [ProcedureStates.Asignado] = S(ProcedureStates.QValidacion, ProcedureStates.Borrador, ProcedureStates.Anulado),
            [ProcedureStates.QValidacion] = S(ProcedureStates.Pendiente, ProcedureStates.Rechazado, ProcedureStates.Borrador, ProcedureStates.Anulado),
            [ProcedureStates.Pendiente] = S(ProcedureStates.Aprobado, ProcedureStates.Rechazado, ProcedureStates.Anulado),
            [ProcedureStates.Aprobado] = S(ProcedureStates.Enviado, ProcedureStates.Anulado),
            [ProcedureStates.Enviado] = S(ProcedureStates.Entregado, ProcedureStates.Rechazado),
            [ProcedureStates.Rechazado] = S(ProcedureStates.Borrador, ProcedureStates.Anulado),
            [ProcedureStates.Entregado] = S(),
            [ProcedureStates.Anulado] = S(),
        };

    /// <summary>
    /// Devuelve <c>true</c> si la transición <paramref name="fromState"/> → <paramref name="toState"/>
    /// está permitida por la máquina de estados.
    /// </summary>
    public static bool CanTransition(string fromState, string toState) =>
        Allowed.TryGetValue(fromState, out var targets) &&
        targets.Contains(toState);

    /// <summary>Devuelve los estados a los que se puede transitar desde <paramref name="fromState"/>.</summary>
    public static IReadOnlyList<string> GetAllowedTargets(string fromState) =>
        Allowed.TryGetValue(fromState, out var targets) ? [.. targets] : [];

    /// <summary>
    /// Intenta hacer la transición; retorna un error tipado si está bloqueada.
    /// </summary>
    public static ProcedureInstanceError? Validate(string fromState, string toState) =>
        CanTransition(fromState, toState)
            ? null
            : ProcedureInstanceError.InvalidTransition(fromState, toState);
}
