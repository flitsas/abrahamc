namespace Flit.Modules.Procedures.Domain;

/// <summary>Formato compuesto #10079: <c>{TYPE}-{TENANT}_{OT}-{SEQ}</c> e.g. <c>TRASP-02_EVE-8841</c>.</summary>
public static class ProcedureReferenceFormatter
{
    private static readonly Dictionary<string, string> KnownTypeAbbreviations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["TRA_ESTANDAR"] = "TRASP",
            ["MAT_LEASING"] = "MATLE",
            ["CAMBIO_COLOR"] = "CAMCO",
        };

    public static string GenerateReferenceNumber(
        string typeCode,
        string tenantCode,
        string otCode,
        int sequence)
    {
        var type = SanitizeSegment(typeCode, 5);
        var tenant = SanitizeSegment(tenantCode, 8);
        var ot = SanitizeSegment(otCode, 8);
        return $"{type}-{tenant}_{ot}-{sequence}";
    }

    public static string AbbreviateProcedureTypeCode(string procedureTypeCode)
    {
        if (KnownTypeAbbreviations.TryGetValue(procedureTypeCode.Trim(), out var known))
        {
            return known;
        }

        var compact = procedureTypeCode.Replace("_", "", StringComparison.Ordinal);
        return SanitizeSegment(compact, 5);
    }

    private static string SanitizeSegment(string value, int maxLength)
    {
        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length == 0)
        {
            return "X";
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
