using System.Text.Json;
using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Application;

/// <summary>Persiste valores del formulario en <c>procedures.procedure_field_values</c>.</summary>
public static class SaveProcedureFieldValues
{
    public static async Task HandleAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid userId,
        string? edgeRole,
        IReadOnlyDictionary<string, string?>? fieldValues,
        IProcedureFieldValueRepository repository,
        CancellationToken ct = default)
    {
        if (fieldValues is null || fieldValues.Count == 0)
        {
            return;
        }

        var entries = fieldValues
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .Select(kv => new ProcedureFieldValueEntry(
                Guid.CreateVersion7(),
                tenantId,
                procedureInstanceId,
                edgeRole,
                kv.Key.Trim(),
                JsonSerializer.Serialize(kv.Value),
                "text",
                userId,
                userId))
            .ToList();

        if (entries.Count > 0)
        {
            await repository.SaveManyAsync(entries, ct);
        }
    }
}
