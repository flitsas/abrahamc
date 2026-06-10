using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public interface IProcedureDocumentRepository
{
    Task<GeneratedProcedureDocumentRecord> AddGeneratedAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid documentTypeId,
        Guid templateVersionId,
        Guid fileId,
        string dataSnapshotJson,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<GeneratedProcedureDocumentRecord>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
