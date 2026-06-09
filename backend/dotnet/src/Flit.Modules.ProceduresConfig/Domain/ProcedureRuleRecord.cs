using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Regla de negocio lista para evaluación (origen: BD viva o snapshot inmutable).</summary>
public sealed record ProcedureRuleRecord(
    Guid Id,
    string Name,
    int Priority,
    JsonElement ConditionTree,
    JsonElement Actions);
