using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>Resuelve config efectiva global → tenant → OT (ADR-0009).</summary>
public static class ProcedureConfigurationResolver
{
    public static ResolvedProcedureConfiguration? Resolve(ProcedureConfigurationBundle bundle)
    {
        if (!bundle.GlobalIsActive)
        {
            return null;
        }

        if (bundle.TenantActivation is null)
        {
            return null;
        }

        var layers = new List<string> { "global", "company" };
        var edges = bundle.Edges.Select(CloneEdge).ToList();
        var sections = bundle.Sections.Select(CloneSection).ToList();
        var queries = bundle.Queries.Select(CloneQuery).ToList();

        ApplyTenantOverrides(edges, sections, bundle.TenantActivation.OverridesJson);

        var hasCompanyOverrides = HasMeaningfulOverrides(bundle.TenantActivation.OverridesJson);

        if (bundle.OtActivation is not null)
        {
            layers.Add("ot");
            ApplyOtOverrides(queries, bundle.OtActivation.OverridesJson);
        }
        else if (hasCompanyOverrides)
        {
            layers[^1] = "company";
        }
        else
        {
            layers.RemoveAt(layers.Count - 1);
        }

        var scope = ResolveEffectiveScope(layers);

        edges = edges.Where(e => e.IsActive).OrderBy(e => e.DisplayOrder).ToList();
        sections = sections
            .Where(s => s.EdgeCode is null || edges.Any(e => e.Code == s.EdgeCode && e.IsActive))
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        return new ResolvedProcedureConfiguration(
            bundle.ProcedureTypeId,
            bundle.Code,
            bundle.Slug,
            bundle.Name,
            bundle.FamilyCode,
            bundle.MaxSteps,
            scope,
            layers,
            edges,
            sections,
            queries.OrderBy(q => q.DisplayOrder).ToList(),
            bundle.RequiredDocuments);
    }

    /// <summary>Capa más específica aplicada: ot &gt; company &gt; global (HU #9695 AC3).</summary>
    internal static string ResolveEffectiveScope(IReadOnlyList<string> layers)
    {
        if (layers.Count == 0)
        {
            return "global";
        }

        return layers[^1] switch
        {
            "ot" => "ot",
            "company" => "company",
            _ => "global",
        };
    }

    private static bool HasMeaningfulOverrides(string overridesJson) =>
        !string.IsNullOrWhiteSpace(overridesJson) &&
        overridesJson.Trim() is not ("{}" or "null");

    private static void ApplyTenantOverrides(
        List<ProcedureEdgeConfig> edges,
        List<ProcedureFormSectionConfig> sections,
        string overridesJson)
    {
        if (string.IsNullOrWhiteSpace(overridesJson) || overridesJson == "{}")
        {
            return;
        }

        using var doc = JsonDocument.Parse(overridesJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("edges", out var edgesNode) && edgesNode.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in edgesNode.EnumerateObject())
            {
                var idx = FindEdgeIndex(edges, prop.Name);
                if (idx < 0)
                {
                    continue;
                }

                var edge = edges[idx];
                if (prop.Value.TryGetProperty("roleLabel", out var roleLabel))
                {
                    edges[idx] = edge with { RoleLabel = roleLabel.GetString() };
                }

                if (prop.Value.TryGetProperty("isActive", out var isActive) &&
                    (isActive.ValueKind == JsonValueKind.True || isActive.ValueKind == JsonValueKind.False))
                {
                    edges[idx] = edges[idx] with { IsActive = isActive.GetBoolean() };
                }
            }
        }

        if (root.TryGetProperty("disabled_edges", out var disabled) && disabled.ValueKind == JsonValueKind.Array)
        {
            foreach (var code in disabled.EnumerateArray())
            {
                var idx = FindEdgeIndex(edges, code.GetString() ?? string.Empty);
                if (idx >= 0)
                {
                    edges[idx] = edges[idx] with { IsActive = false };
                }
            }
        }

        if (root.TryGetProperty("sections", out var sectionsNode) && sectionsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sectionsNode.EnumerateArray())
            {
                if (!item.TryGetProperty("sectionKey", out var keyEl))
                {
                    continue;
                }

                var key = keyEl.GetString();
                var sectionIdx = sections.ToList().FindIndex(s => s.SectionKey == key);
                if (sectionIdx < 0 || !item.TryGetProperty("title", out var titleEl))
                {
                    continue;
                }

                var section = sections[sectionIdx];
                sections[sectionIdx] = section with { Title = titleEl.GetString() ?? section.Title };
            }
        }
    }

    private static void ApplyOtOverrides(List<ProcedureQueryConfig> queries, string overridesJson)
    {
        if (string.IsNullOrWhiteSpace(overridesJson) || overridesJson == "{}")
        {
            return;
        }

        using var doc = JsonDocument.Parse(overridesJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("queries", out var queriesNode) || queriesNode.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in queriesNode.EnumerateArray())
        {
            var connector = item.TryGetProperty("connector", out var c) ? c.GetString() : null;
            var edge = item.TryGetProperty("edge", out var e) ? e.GetString() : null;
            var personKind = item.TryGetProperty("personKind", out var p) ? p.GetString() : "any";
            if (connector is null)
            {
                continue;
            }

            for (var i = 0; i < queries.Count; i++)
            {
                var q = queries[i];
                if (!string.Equals(q.ConnectorCode, connector, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(q.EdgeCode ?? string.Empty, edge ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(q.PersonKindFilter, personKind ?? "any", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.TryGetProperty("isMandatory", out var mandatory) &&
                    (mandatory.ValueKind == JsonValueKind.True || mandatory.ValueKind == JsonValueKind.False))
                {
                    queries[i] = q with { IsMandatory = mandatory.GetBoolean() };
                }
            }
        }
    }

    private static int FindEdgeIndex(List<ProcedureEdgeConfig> edges, string code) =>
        edges.FindIndex(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));

    private static ProcedureEdgeConfig CloneEdge(ProcedureEdgeConfig e) => e with { };

    private static ProcedureFormSectionConfig CloneSection(ProcedureFormSectionConfig s) =>
        s with { Fields = s.Fields.ToList() };

    private static ProcedureQueryConfig CloneQuery(ProcedureQueryConfig q) => q with { };
}
