using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public sealed record AdminProcedureTypeSummary(
    Guid Id,
    string Code,
    string Name,
    string FamilyCode,
    bool GlobalIsActive,
    bool TenantActivationActive,
    int ActiveEdgeCount,
    int MaxSteps);

public sealed record AdminRequiredDocumentItem(
    Guid Id,
    string DocumentTypeCode,
    string DocumentTypeName,
    string? EdgeCode,
    string Kind,
    bool IsRequired,
    int DisplayOrder,
    string? ActorRole,
    bool IsActive);

public sealed record AdminCatalogDocumentType(
    string Code,
    string Name,
    string? DefaultPersonKind,
    int DisplayOrder,
    bool IsActive = true);

public sealed record CreateCatalogDocumentTypeCommand(
    Guid TenantId,
    string Code,
    string Name,
    string? DefaultPersonKind,
    int DisplayOrder);

public sealed record UpdateCatalogDocumentTypeCommand(
    Guid TenantId,
    string Code,
    string? Name,
    int? DisplayOrder,
    bool? IsActive);

public sealed record CreateRequiredDocumentCommand(
    Guid TenantId,
    string ProcedureTypeCode,
    string EdgeCode,
    string DocumentTypeCode,
    string Kind,
    bool IsRequired,
    int DisplayOrder,
    string? ActorRole);

public sealed record UpdateRequiredDocumentCommand(
    Guid TenantId,
    string ProcedureTypeCode,
    Guid DocumentId,
    string? EdgeCode,
    string? Kind,
    bool? IsRequired,
    int? DisplayOrder,
    string? ActorRole,
    bool? IsActive);

public sealed record AdminProcedureMatrixView(
    Guid ProcedureTypeId,
    string Code,
    string Name,
    string FamilyCode,
    int MaxSteps,
    bool GlobalIsActive,
    bool TenantActivationActive,
    IReadOnlyList<ProcedureEdgeConfig> Edges,
    IReadOnlyList<ProcedureFormSectionConfig> Sections,
    IReadOnlyList<AdminRequiredDocumentItem> RequiredDocuments);

public sealed record AdminCatalogFamily(Guid Id, string Code, string Name, int DisplayOrder);

public sealed record CreateCatalogFamilyCommand(string Code, string Name, int DisplayOrder);

public enum DeleteCatalogFamilyErrorKind
{
    NotFound,
    FamilyInUse,
}

public sealed record DeleteCatalogFamilyError(DeleteCatalogFamilyErrorKind Kind, string Message);

public sealed record AdminCatalogEdge(
    string Code,
    string Name,
    string EdgeKind,
    int DisplayOrder);

public sealed record AdminQueryConfigItem(
    Guid Id,
    string EdgeCode,
    string ConnectorCode,
    string ConnectorName,
    bool IsMandatory,
    bool IsOmitible,
    string PersonKindFilter,
    int DisplayOrder);

public sealed record CreateQueryConfigCommand(
    Guid TenantId,
    string ProcedureTypeCode,
    string EdgeCode,
    string ConnectorCode,
    bool IsMandatory,
    bool IsOmitible,
    string PersonKindFilter,
    int DisplayOrder);

public sealed record UpdateQueryConfigCommand(
    Guid TenantId,
    string ProcedureTypeCode,
    Guid QueryConfigId,
    bool? IsMandatory,
    bool? IsOmitible,
    string? PersonKindFilter,
    int? DisplayOrder);

public sealed record CreateProcedureTypeEdgeInput(
    string EdgeCode,
    bool IsActive,
    bool IsRequired,
    int DisplayOrder,
    string? RoleLabel);

public sealed record CreateProcedureTypeCommand(
    Guid TenantId,
    Guid? TrafficAgencyId,
    string FamilyCode,
    string Code,
    string Slug,
    string Name,
    int MaxSteps,
    IReadOnlyList<CreateProcedureTypeEdgeInput> Edges);

public sealed record CreateProcedureTypeResult(
    Guid ProcedureTypeId,
    string Code,
    string Slug,
    string Name);

public enum CreateProcedureTypeErrorKind
{
    Validation,
    Conflict,
    NotFound,
}

public sealed record CreateProcedureTypeError(CreateProcedureTypeErrorKind Kind, string Message);

public interface IProceduresConfigAdminRepository
{
    Task<IReadOnlyList<AdminProcedureTypeSummary>> ListTypesForAdminAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default);

    Task<AdminProcedureMatrixView?> GetMatrixAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default);

    Task<bool> SetEdgeActiveAsync(
        Guid tenantId,
        string procedureTypeCode,
        string edgeCode,
        bool isActive,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminCatalogFamily>> ListCatalogFamiliesAsync(CancellationToken ct = default);

    Task<(AdminCatalogFamily? Ok, string? Error)> CreateCatalogFamilyAsync(
        CreateCatalogFamilyCommand command,
        CancellationToken ct = default);

    Task<(bool Ok, DeleteCatalogFamilyError? Error)> DeleteCatalogFamilyAsync(
        string familyCode,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogEdgesAsync(CancellationToken ct = default);

    Task<(CreateProcedureTypeResult? Ok, CreateProcedureTypeError? Error)> CreateProcedureTypeAsync(
        CreateProcedureTypeCommand command,
        CancellationToken ct = default);

    Task<bool> SetTenantActivationAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        bool isActive,
        CancellationToken ct = default);

    Task<bool> SetGlobalTypeActiveAsync(
        Guid tenantId,
        string procedureTypeCode,
        bool isActive,
        CancellationToken ct = default);

    Task<bool> UpdateProcedureTypeAsync(
        Guid tenantId,
        string procedureTypeCode,
        string? name,
        int? maxSteps,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminCatalogDocumentType>> ListCatalogDocumentTypesAsync(
        CancellationToken ct = default);

    Task<(AdminCatalogDocumentType? Ok, string? Error)> CreateCatalogDocumentTypeAsync(
        CreateCatalogDocumentTypeCommand command,
        CancellationToken ct = default);

    Task<(AdminCatalogDocumentType? Ok, string? Error)> UpdateCatalogDocumentTypeAsync(
        UpdateCatalogDocumentTypeCommand command,
        CancellationToken ct = default);

    Task<bool> DeactivateCatalogDocumentTypeAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default);

    Task<(AdminRequiredDocumentItem? Ok, string? Error)> CreateRequiredDocumentAsync(
        CreateRequiredDocumentCommand command,
        CancellationToken ct = default);

    Task<(AdminRequiredDocumentItem? Ok, string? Error)> UpdateRequiredDocumentAsync(
        UpdateRequiredDocumentCommand command,
        CancellationToken ct = default);

    Task<bool> DeactivateRequiredDocumentAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid documentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogQueryConnectorsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AdminQueryConfigItem>> ListQueryConfigsAsync(
        Guid tenantId,
        string procedureTypeCode,
        CancellationToken ct = default);

    Task<(AdminQueryConfigItem? Ok, string? Error)> CreateQueryConfigAsync(
        CreateQueryConfigCommand command,
        CancellationToken ct = default);

    Task<(AdminQueryConfigItem? Ok, string? Error)> UpdateQueryConfigAsync(
        UpdateQueryConfigCommand command,
        CancellationToken ct = default);

    Task<bool> DeleteQueryConfigAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid queryConfigId,
        CancellationToken ct = default);
}
