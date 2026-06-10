using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public interface IDocumentTemplateRepository
{
    Task<DocumentTemplateRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<DocumentTemplateRecord?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentTemplateRecord>> ListAsync(CancellationToken ct = default);

    Task<DocumentTemplateRecord> CreateAsync(DocumentTemplateWriteModel model, CancellationToken ct = default);

    Task<DocumentTemplateVersionRecord?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default);

    Task<DocumentTemplateVersionRecord> AddVersionAsync(
        DocumentTemplateVersionWriteModel model,
        CancellationToken ct = default);

    Task<ResultPublish> PublishVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);

    Task<UpdateMarkerMapResult> TryUpdateMarkerMapAsync(
        Guid versionId,
        JsonElement markerMap,
        Guid actorUserId,
        CancellationToken ct = default);
}

public enum ResultPublish
{
    Ok,
    NotFound,
    AlreadyPublished,
}

public enum UpdateMarkerMapResult
{
    Ok,
    NotFound,
    ImmutableVersion,
}
