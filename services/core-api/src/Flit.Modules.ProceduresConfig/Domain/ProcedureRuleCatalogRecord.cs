using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Registro de procedures_config.rules para CRUD del constructor (RGL-04 #9440).</summary>
public sealed record ProcedureRuleCatalogRecord(
    Guid Id,
    Guid TenantId,
    Guid ProcedureTypeId,
    string Name,
    string? Description,
    JsonElement ConditionTree,
    JsonElement Actions,
    int Priority,
    bool IsActive,
    int RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
