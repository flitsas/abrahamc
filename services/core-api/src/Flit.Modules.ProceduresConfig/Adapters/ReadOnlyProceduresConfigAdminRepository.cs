using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

/// <summary>Admin de matriz en modo lectura cuando la persistencia es PostgreSQL (PATCH no soportado aún).</summary>
public sealed class ReadOnlyProceduresConfigAdminRepository(IProceduresConfigReadRepository readRepo)
    : IProceduresConfigAdminRepository
{
    public async Task<IReadOnlyList<AdminProcedureTypeSummary>> ListTypesForAdminAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var types = await readRepo.ListEffectiveActiveTypesAsync(tenantId, trafficAgencyId, ct);
        return types
            .Select(t => new AdminProcedureTypeSummary(
                t.Id,
                t.Code,
                t.Name,
                t.FamilyCode,
                GlobalIsActive: true,
                TenantActivationActive: true,
                ActiveEdgeCount: t.MaxSteps,
                t.MaxSteps))
            .ToList();
    }

    public async Task<AdminProcedureMatrixView?> GetMatrixAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var bundle = await readRepo.LoadConfigurationBundleAsync(
            tenantId,
            procedureTypeCode,
            trafficAgencyId,
            ct);

        if (bundle is null)
        {
            return null;
        }

        return new AdminProcedureMatrixView(
            bundle.ProcedureTypeId,
            bundle.Code,
            bundle.Name,
            bundle.FamilyCode,
            bundle.MaxSteps,
            bundle.GlobalIsActive,
            TenantActivationActive: true,
            bundle.Edges,
            bundle.Sections,
            bundle.RequiredDocuments.Select(d => new AdminRequiredDocumentItem(
                Guid.Empty,
                d.DocumentTypeCode,
                d.DocumentTypeCode,
                d.EdgeCode,
                d.Kind,
                d.IsRequired,
                d.DisplayOrder,
                d.ActorRole,
                IsActive: true)).ToList());
    }

    public Task<bool> SetTenantActivationAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        bool isActive,
        CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> SetGlobalTypeActiveAsync(
        Guid tenantId,
        string procedureTypeCode,
        bool isActive,
        CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> UpdateProcedureTypeAsync(
        Guid tenantId,
        string procedureTypeCode,
        string? name,
        int? maxSteps,
        CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> SetEdgeActiveAsync(
        Guid tenantId,
        string procedureTypeCode,
        string edgeCode,
        bool isActive,
        CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<IReadOnlyList<AdminCatalogFamily>> ListCatalogFamiliesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogFamily>>([]);

    public Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogEdgesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogEdge>>([]);

    public Task<(CreateProcedureTypeResult? Ok, CreateProcedureTypeError? Error)> CreateProcedureTypeAsync(
        CreateProcedureTypeCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(CreateProcedureTypeResult?, CreateProcedureTypeError?)>((
            null,
            new CreateProcedureTypeError(
                CreateProcedureTypeErrorKind.Validation,
                "Crear tipos requiere repositorio Npgsql con permisos de escritura.")));

    public Task<IReadOnlyList<AdminCatalogDocumentType>> ListCatalogDocumentTypesAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogDocumentType>>([]);

    public Task<(AdminCatalogDocumentType? Ok, string? Error)> CreateCatalogDocumentTypeAsync(
        CreateCatalogDocumentTypeCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(AdminCatalogDocumentType?, string?)>((null, "Requiere PostgreSQL."));

    public Task<(AdminCatalogDocumentType? Ok, string? Error)> UpdateCatalogDocumentTypeAsync(
        UpdateCatalogDocumentTypeCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(AdminCatalogDocumentType?, string?)>((null, "Requiere PostgreSQL."));

    public Task<bool> DeactivateCatalogDocumentTypeAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<(AdminRequiredDocumentItem? Ok, string? Error)> CreateRequiredDocumentAsync(
        CreateRequiredDocumentCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(AdminRequiredDocumentItem?, string?)>((null, "Requiere PostgreSQL."));

    public Task<(AdminRequiredDocumentItem? Ok, string? Error)> UpdateRequiredDocumentAsync(
        UpdateRequiredDocumentCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(AdminRequiredDocumentItem?, string?)>((null, "Requiere PostgreSQL."));

    public Task<bool> DeactivateRequiredDocumentAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid documentId,
        CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogQueryConnectorsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogEdge>>([]);

    public Task<IReadOnlyList<AdminQueryConfigItem>> ListQueryConfigsAsync(
        Guid tenantId,
        string procedureTypeCode,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminQueryConfigItem>>([]);

    public Task<(AdminQueryConfigItem? Ok, string? Error)> CreateQueryConfigAsync(
        CreateQueryConfigCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(AdminQueryConfigItem?, string?)>((null, "Requiere PostgreSQL."));

    public Task<(AdminQueryConfigItem? Ok, string? Error)> UpdateQueryConfigAsync(
        UpdateQueryConfigCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(AdminQueryConfigItem?, string?)>((null, "Requiere PostgreSQL."));

    public Task<bool> DeleteQueryConfigAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid queryConfigId,
        CancellationToken ct = default) =>
        Task.FromResult(false);
}
