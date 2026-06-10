namespace Flit.Modules.Procedures.Domain;

/// <summary>Categorías de error de dominio para instancias de trámite.</summary>
public enum ProcedureInstanceErrorKind
{
    InvalidStateTransition,
    InvalidState,
    NotFound,
    NotEditable,
}

/// <summary>Error de dominio tipado para <c>procedure_instances</c>.</summary>
public sealed record ProcedureInstanceError(
    ProcedureInstanceErrorKind Kind,
    string Message)
{
    public static ProcedureInstanceError InvalidTransition(string from, string to) =>
        new(ProcedureInstanceErrorKind.InvalidStateTransition,
            $"Transición de estado inválida: {from} -> {to}");

    public static ProcedureInstanceError InvalidState(string state) =>
        new(ProcedureInstanceErrorKind.InvalidState,
            $"Estado desconocido: {state}");

    public static ProcedureInstanceError NotEditable(string currentState) =>
        new(ProcedureInstanceErrorKind.NotEditable,
            $"El trámite en estado '{currentState}' no es editable (solo en 'borrador').");
}
