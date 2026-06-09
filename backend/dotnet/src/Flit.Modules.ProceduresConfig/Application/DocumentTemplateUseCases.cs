using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.ProceduresConfig.Application;

public static class CreateDocumentTemplate
{
    public sealed record Command(
        string Code,
        string Name,
        Guid DocumentTypeId,
        string Scope,
        Guid? TenantId,
        Guid ActorUserId);

    public static async Task<Result<DocumentTemplateRecord, DocumentTemplateError>> HandleAsync(
        Command command,
        IDocumentTemplateRepository repo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<DocumentTemplateRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Validation, "code y name son obligatorios."));
        }

        if (command.Scope is not ("global" or "tenant"))
        {
            return Result<DocumentTemplateRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Validation, "scope debe ser global o tenant."));
        }

        if (command.Scope == "tenant" && command.TenantId is null)
        {
            return Result<DocumentTemplateRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Validation, "tenant_id es obligatorio para scope tenant."));
        }

        var existing = await repo.GetByCodeAsync(command.Code.Trim(), ct);
        if (existing is not null)
        {
            return Result<DocumentTemplateRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Conflict, $"Ya existe una plantilla con code '{command.Code}'."));
        }

        var created = await repo.CreateAsync(
            new DocumentTemplateWriteModel(
                command.Code.Trim(),
                command.Name.Trim(),
                command.DocumentTypeId,
                command.Scope,
                command.TenantId,
                command.ActorUserId),
            ct);

        return Result<DocumentTemplateRecord, DocumentTemplateError>.Success(created);
    }
}

public static class AddDocumentTemplateVersion
{
    public sealed record Command(
        Guid TemplateId,
        string BodyInline,
        JsonElement MarkerMap,
        Guid ActorUserId);

    public static async Task<Result<DocumentTemplateVersionRecord, DocumentTemplateError>> HandleAsync(
        Command command,
        IDocumentTemplateRepository repo,
        CancellationToken ct = default)
    {
        if (command.MarkerMap.ValueKind != JsonValueKind.Object)
        {
            return Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Validation, "marker_map debe ser un objeto JSON."));
        }

        if (string.IsNullOrWhiteSpace(command.BodyInline))
        {
            return Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Validation, "body_inline es obligatorio."));
        }

        var template = await repo.GetByIdAsync(command.TemplateId, ct);
        if (template is null)
        {
            return Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Plantilla no encontrada."));
        }

        var version = await repo.AddVersionAsync(
            new DocumentTemplateVersionWriteModel(
                command.TemplateId,
                command.BodyInline,
                command.MarkerMap,
                command.ActorUserId),
            ct);

        return Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Success(version);
    }
}

public static class PublishDocumentTemplateVersion
{
    public sealed record Command(Guid VersionId, Guid ActorUserId);

    public static async Task<Result<DocumentTemplateVersionRecord, DocumentTemplateError>> HandleAsync(
        Command command,
        IDocumentTemplateRepository repo,
        CancellationToken ct = default)
    {
        var publish = await repo.PublishVersionAsync(command.VersionId, command.ActorUserId, ct);
        return publish switch
        {
            ResultPublish.NotFound => Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Versión no encontrada.")),
            ResultPublish.AlreadyPublished => Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Conflict, "La versión ya está publicada.")),
            _ => await DocumentTemplateVersionLoader.LoadAsync(repo, command.VersionId, ct),
        };
    }
}

internal static class DocumentTemplateVersionLoader
{
    public static async Task<Result<DocumentTemplateVersionRecord, DocumentTemplateError>> LoadAsync(
        IDocumentTemplateRepository repo,
        Guid versionId,
        CancellationToken ct)
    {
        var version = await repo.GetVersionByIdAsync(versionId, ct);
        return version is null
            ? Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Versión no encontrada."))
            : Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Success(version);
    }
}

public static class UpdateDocumentTemplateVersionMarkerMap
{
    public sealed record Command(Guid VersionId, JsonElement MarkerMap, Guid ActorUserId);

    public static async Task<Result<DocumentTemplateVersionRecord, DocumentTemplateError>> HandleAsync(
        Command command,
        IDocumentTemplateRepository repo,
        CancellationToken ct = default)
    {
        if (command.MarkerMap.ValueKind != JsonValueKind.Object)
        {
            return Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.Validation, "marker_map debe ser un objeto JSON."));
        }

        var result = await repo.TryUpdateMarkerMapAsync(command.VersionId, command.MarkerMap, command.ActorUserId, ct);
        return result switch
        {
            UpdateMarkerMapResult.NotFound => Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Versión no encontrada.")),
            UpdateMarkerMapResult.ImmutableVersion => Result<DocumentTemplateVersionRecord, DocumentTemplateError>.Failure(
                new DocumentTemplateError(
                    DocumentTemplateErrorKind.ImmutableVersion,
                    "No se puede modificar marker_map de una versión publicada; cree una nueva versión.")),
            _ => await DocumentTemplateVersionLoader.LoadAsync(repo, command.VersionId, ct),
        };
    }
}
