using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9429 — SIMIT obligatorio en Locatario/Leasing; firma/RTM omitibles.</summary>
public static class LeasingLocatarioRules
{
    private static readonly string[] OmitibleConnectors = ["FIRMA", "RTM"];

    public static List<QueryExecutionPlan> Enforce(
        string procedureTypeCode,
        string edgeCode,
        List<QueryExecutionPlan> queries)
    {
        if (!string.Equals(procedureTypeCode, "MAT_LEASING", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(edgeCode, "locatario", StringComparison.OrdinalIgnoreCase))
        {
            return queries;
        }

        var result = new List<QueryExecutionPlan>();
        foreach (var q in queries)
        {
            if (string.Equals(q.ConnectorCode, "SIMIT", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(q with { IsMandatory = true, IsOmitible = false });
                continue;
            }

            if (OmitibleConnectors.Contains(q.ConnectorCode, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(q with { IsMandatory = false, IsOmitible = true });
                continue;
            }

            result.Add(q);
        }

        foreach (var omit in OmitibleConnectors)
        {
            if (result.All(r => !string.Equals(r.ConnectorCode, omit, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new QueryExecutionPlan(omit, edgeCode, false, true, "any"));
            }
        }

        return result;
    }

    public static bool RejectsOmissionOfMandatorySimit(
        string procedureTypeCode,
        string edgeCode,
        IReadOnlyList<string> omittedConnectorCodes) =>
        string.Equals(procedureTypeCode, "MAT_LEASING", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(edgeCode, "locatario", StringComparison.OrdinalIgnoreCase) &&
        omittedConnectorCodes.Any(c => string.Equals(c, "SIMIT", StringComparison.OrdinalIgnoreCase));
}
