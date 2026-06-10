using System.Collections.Concurrent;
using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryDocumentTemplateRepository : IDocumentTemplateRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentTemplateRecord> _templates = new();
    private readonly ConcurrentDictionary<Guid, DocumentTemplateVersionRecord> _versions = new();

    public Task<DocumentTemplateRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_templates.TryGetValue(id, out var t) ? t : null);

    public Task<DocumentTemplateRecord?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var found = _templates.Values.FirstOrDefault(t =>
            string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<DocumentTemplateRecord>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DocumentTemplateRecord>>(_templates.Values.OrderBy(t => t.Code).ToList());

    public Task<DocumentTemplateRecord> CreateAsync(DocumentTemplateWriteModel model, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var record = new DocumentTemplateRecord(
            id,
            model.Code,
            model.Name,
            model.DocumentTypeId,
            model.Scope,
            model.TenantId,
            CurrentVersion: 0,
            IsActive: true,
            RowVersion: 1);
        _templates[id] = record;
        return Task.FromResult(record);
    }

    public Task<DocumentTemplateVersionRecord?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default) =>
        Task.FromResult(_versions.TryGetValue(versionId, out var v) ? v : null);

    public Task<DocumentTemplateVersionRecord> AddVersionAsync(
        DocumentTemplateVersionWriteModel model,
        CancellationToken ct = default)
    {
        if (!_templates.TryGetValue(model.TemplateId, out var template))
            throw new InvalidOperationException("Plantilla no encontrada.");

        var nextVersion = template.CurrentVersion + 1;
        var versionId = Guid.NewGuid();
        var record = new DocumentTemplateVersionRecord(
            versionId,
            model.TemplateId,
            nextVersion,
            model.BodyInline,
            model.MarkerMap.Clone(),
            IsCurrent: false,
            PublishedAt: null,
            RowVersion: 1);

        _versions[versionId] = record;
        _templates[model.TemplateId] = template with { CurrentVersion = nextVersion };
        return Task.FromResult(record);
    }

    public Task<ResultPublish> PublishVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        if (!_versions.TryGetValue(versionId, out var version))
            return Task.FromResult(ResultPublish.NotFound);

        if (version.PublishedAt is not null)
            return Task.FromResult(ResultPublish.AlreadyPublished);

        var published = version with
        {
            PublishedAt = DateTimeOffset.UtcNow,
            IsCurrent = true,
        };
        _versions[versionId] = published;

        foreach (var key in _versions.Keys.ToList())
        {
            if (_versions[key].TemplateId == version.TemplateId && key != versionId)
                _versions[key] = _versions[key] with { IsCurrent = false };
        }

        return Task.FromResult(ResultPublish.Ok);
    }

    public Task<UpdateMarkerMapResult> TryUpdateMarkerMapAsync(
        Guid versionId,
        JsonElement markerMap,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        if (!_versions.TryGetValue(versionId, out var version))
            return Task.FromResult(UpdateMarkerMapResult.NotFound);

        if (version.PublishedAt is not null)
            return Task.FromResult(UpdateMarkerMapResult.ImmutableVersion);

        _versions[versionId] = version with { MarkerMap = markerMap.Clone() };
        return Task.FromResult(UpdateMarkerMapResult.Ok);
    }
}
