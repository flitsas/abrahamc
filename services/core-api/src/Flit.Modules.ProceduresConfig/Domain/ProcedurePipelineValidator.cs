namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Valida pipeline mínimo de pasos activos en matriz de trámites (HU #9692 AC2).</summary>
public static class ProcedurePipelineValidator
{
    public const int MinActiveDataEdges = 4;
    public const string DocumentsEdgeCode = "documentos";

    public static (bool IsValid, string? ErrorMessage) ValidateMinActiveDataEdges(
        IEnumerable<(string EdgeCode, bool IsActive)> edges)
    {
        var activeDataEdgeCount = edges.Count(e =>
            e.IsActive &&
            !string.Equals(e.EdgeCode, DocumentsEdgeCode, StringComparison.OrdinalIgnoreCase));

        if (activeDataEdgeCount < MinActiveDataEdges)
        {
            return (false,
                $"El pipeline debe incluir al menos {MinActiveDataEdges} pasos activos de datos (excluyendo documentos). Actual: {activeDataEdgeCount}.");
        }

        return (true, null);
    }
}
