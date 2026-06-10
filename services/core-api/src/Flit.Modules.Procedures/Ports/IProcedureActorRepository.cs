namespace Flit.Modules.Procedures.Ports;

public sealed record ProcedureActorEntry(
    Guid Id,
    Guid TenantId,
    Guid ProcedureInstanceId,
    string EdgeRole,
    string PersonKind,
    string DocumentTypeCode,
    string DocumentNumber,
    string? FullName,
    Guid CreatedBy,
    Guid UpdatedBy);

public interface IProcedureActorRepository
{
    Task UpsertAsync(ProcedureActorEntry entry, CancellationToken ct = default);

    Task<ProcedureActorEntry?> GetByInstanceAndEdgeAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        string edgeRole,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProcedureActorEntry>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
