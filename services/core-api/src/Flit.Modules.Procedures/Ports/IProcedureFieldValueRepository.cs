namespace Flit.Modules.Procedures.Ports;

public sealed record ProcedureFieldValueEntry(
    Guid Id,
    Guid TenantId,
    Guid ProcedureInstanceId,
    string? EdgeRole,
    string FieldKey,
    string ValueJson,
    string DataType,
    Guid CreatedBy,
    Guid UpdatedBy);

public interface IProcedureFieldValueRepository
{
    Task SaveManyAsync(IReadOnlyList<ProcedureFieldValueEntry> entries, CancellationToken ct = default);
}
