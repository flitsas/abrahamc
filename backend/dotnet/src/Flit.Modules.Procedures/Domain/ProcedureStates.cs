namespace Flit.Modules.Procedures.Domain;

/// <summary>
/// Constantes de estado de <c>procedures.procedure_instances.state</c>.
/// Espejo del CHECK constraint y del trigger <c>procedures.validate_state_transition()</c>.
/// </summary>
public static class ProcedureStates
{
    public const string Borrador = "borrador";
    public const string Asignado = "asignado";
    public const string QValidacion = "q_validacion";
    public const string Pendiente = "pendiente";
    public const string Aprobado = "aprobado";
    public const string Enviado = "enviado";
    public const string Entregado = "entregado";
    public const string Rechazado = "rechazado";
    public const string Anulado = "anulado";

    /// <summary>Estados en los que el trámite ya no puede cambiar.</summary>
    public static readonly IReadOnlySet<string> Terminal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Entregado,
            Anulado,
        };

    /// <summary>Todos los estados válidos (alineado con el CHECK constraint de la tabla).</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Borrador, Asignado, QValidacion, Pendiente,
            Aprobado, Enviado, Entregado, Rechazado, Anulado,
        };
}
