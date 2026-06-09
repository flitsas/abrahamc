using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

public sealed record DocumentTemplateRecord(
    Guid Id,
    string Code,
    string Name,
    Guid DocumentTypeId,
    string Scope,
    Guid? TenantId,
    int CurrentVersion,
    bool IsActive,
    int RowVersion);

public sealed record DocumentTemplateVersionRecord(
    Guid Id,
    Guid TemplateId,
    int Version,
    string? BodyInline,
    JsonElement MarkerMap,
    bool IsCurrent,
    DateTimeOffset? PublishedAt,
    int RowVersion);

public sealed record DocumentTemplateWriteModel(
    string Code,
    string Name,
    Guid DocumentTypeId,
    string Scope,
    Guid? TenantId,
    Guid ActorUserId);

public sealed record DocumentTemplateVersionWriteModel(
    Guid TemplateId,
    string BodyInline,
    JsonElement MarkerMap,
    Guid ActorUserId);

public sealed record GeneratedProcedureDocumentRecord(
    Guid Id,
    Guid TenantId,
    Guid ProcedureInstanceId,
    Guid DocumentTypeId,
    Guid TemplateVersionId,
    Guid FileId,
    JsonElement DataSnapshot,
    string Status);
