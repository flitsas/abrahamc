using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

public static class ListAdminProcedureTypes
{
    public sealed record Query(Guid TenantId, Guid? TrafficAgencyId);

    public static Task<IReadOnlyList<AdminProcedureTypeSummary>> HandleAsync(
        Query query,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.ListTypesForAdminAsync(query.TenantId, query.TrafficAgencyId, ct);
}

public static class GetAdminProcedureMatrix
{
    public sealed record Query(Guid TenantId, string ProcedureTypeCode, Guid? TrafficAgencyId);

    public static Task<AdminProcedureMatrixView?> HandleAsync(
        Query query,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.GetMatrixAsync(query.TenantId, query.ProcedureTypeCode, query.TrafficAgencyId, ct);
}

public static class UpdateAdminProcedureEdge
{
    public sealed record Command(
        Guid TenantId,
        string ProcedureTypeCode,
        string EdgeCode,
        bool IsActive);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.SetEdgeActiveAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.EdgeCode,
            command.IsActive,
            ct);
}

public static class ListAdminCatalogFamilies
{
    public static Task<IReadOnlyList<AdminCatalogFamily>> HandleAsync(
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.ListCatalogFamiliesAsync(ct);
}

public static class CreateAdminCatalogFamily
{
    public static Task<(AdminCatalogFamily? Ok, string? Error)> HandleAsync(
        CreateCatalogFamilyCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateCatalogFamilyAsync(command, ct);
}

public static class DeleteAdminCatalogFamily
{
    public static Task<(bool Ok, DeleteCatalogFamilyError? Error)> HandleAsync(
        string familyCode,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.DeleteCatalogFamilyAsync(familyCode, ct);
}

public static class ListAdminCatalogEdges
{
    public static Task<IReadOnlyList<AdminCatalogEdge>> HandleAsync(
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.ListCatalogEdgesAsync(ct);
}

public static class CreateAdminProcedureType
{
    public static Task<(CreateProcedureTypeResult? Ok, CreateProcedureTypeError? Error)> HandleAsync(
        CreateProcedureTypeCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateProcedureTypeAsync(command, ct);
}

public static class UpdateAdminTenantActivation
{
    public sealed record Command(
        Guid TenantId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        bool IsActive);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.SetTenantActivationAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.TrafficAgencyId,
            command.IsActive,
            ct);
}

public static class UpdateAdminProcedureTypeGlobal
{
    public const string ForbiddenNotSuperAdminCode = "SUPER_ADMIN_REQUIRED";

    public sealed record Command(Guid TenantId, string ProcedureTypeCode, bool IsActive);

    /// <summary>HU #9695 AC4 — solo SuperAdmin puede cambiar is_active global.</summary>
    public static string? ValidateSuperAdmin(bool isSuperAdmin) =>
        isSuperAdmin ? null : ForbiddenNotSuperAdminCode;

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.SetGlobalTypeActiveAsync(command.TenantId, command.ProcedureTypeCode, command.IsActive, ct);
}

public static class UpdateAdminProcedureTypeMetadata
{
    public sealed record Command(
        Guid TenantId,
        string ProcedureTypeCode,
        string? Name,
        int? MaxSteps);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.UpdateProcedureTypeAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.Name,
            command.MaxSteps,
            ct);
}

public static class ListAdminCatalogDocumentTypes
{
    public static Task<IReadOnlyList<AdminCatalogDocumentType>> HandleAsync(
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.ListCatalogDocumentTypesAsync(ct);
}

public static class CreateAdminCatalogDocumentType
{
    public static Task<(AdminCatalogDocumentType? Ok, string? Error)> HandleAsync(
        CreateCatalogDocumentTypeCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateCatalogDocumentTypeAsync(command, ct);
}

public static class UpdateAdminCatalogDocumentType
{
    public static Task<(AdminCatalogDocumentType? Ok, string? Error)> HandleAsync(
        UpdateCatalogDocumentTypeCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.UpdateCatalogDocumentTypeAsync(command, ct);
}

public static class DeactivateAdminCatalogDocumentType
{
    public static Task<bool> HandleAsync(
        Guid tenantId,
        string code,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.DeactivateCatalogDocumentTypeAsync(tenantId, code, ct);
}

public static class CreateAdminRequiredDocument
{
    public static Task<(AdminRequiredDocumentItem? Ok, string? Error)> HandleAsync(
        CreateRequiredDocumentCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateRequiredDocumentAsync(command, ct);
}

public static class UpdateAdminRequiredDocument
{
    public static Task<(AdminRequiredDocumentItem? Ok, string? Error)> HandleAsync(
        UpdateRequiredDocumentCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.UpdateRequiredDocumentAsync(command, ct);
}

public static class DeactivateAdminRequiredDocument
{
    public sealed record Command(Guid TenantId, string ProcedureTypeCode, Guid DocumentId);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.DeactivateRequiredDocumentAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.DocumentId,
            ct);
}

public static class ListAdminCatalogQueryConnectors
{
    public static Task<IReadOnlyList<AdminCatalogEdge>> HandleAsync(
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.ListCatalogQueryConnectorsAsync(ct);
}

public static class ListAdminQueryConfigs
{
    public sealed record Query(Guid TenantId, string ProcedureTypeCode);

    public static Task<IReadOnlyList<AdminQueryConfigItem>> HandleAsync(
        Query query,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.ListQueryConfigsAsync(query.TenantId, query.ProcedureTypeCode, ct);
}

public static class CreateAdminQueryConfig
{
    public static Task<(AdminQueryConfigItem? Ok, string? Error)> HandleAsync(
        CreateQueryConfigCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateQueryConfigAsync(command, ct);
}

public static class UpdateAdminQueryConfig
{
    public static Task<(AdminQueryConfigItem? Ok, string? Error)> HandleAsync(
        UpdateQueryConfigCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.UpdateQueryConfigAsync(command, ct);
}

public static class DeleteAdminQueryConfig
{
    public sealed record Command(Guid TenantId, string ProcedureTypeCode, Guid QueryConfigId);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.DeleteQueryConfigAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.QueryConfigId,
            ct);
}

public static class GetAdminEdgeForm
{
    public sealed record Query(Guid TenantId, string ProcedureTypeCode, string EdgeCode);

    public static Task<AdminEdgeFormView?> HandleAsync(
        Query query,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.GetFormByEdgeAsync(query.TenantId, query.ProcedureTypeCode, query.EdgeCode, ct);
}

public static class CreateAdminFormSection
{
    public static Task<(AdminFormSectionItem? Ok, string? Error)> HandleAsync(
        CreateFormSectionCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateFormSectionAsync(command, ct);
}

public static class UpdateAdminFormSection
{
    public static Task<(AdminFormSectionItem? Ok, string? Error)> HandleAsync(
        UpdateFormSectionCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.UpdateFormSectionAsync(command, ct);
}

public static class DeactivateAdminFormSection
{
    public sealed record Command(Guid TenantId, string ProcedureTypeCode, Guid SectionId);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.DeactivateFormSectionAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.SectionId,
            ct);
}

public static class CreateAdminFormField
{
    public static Task<(AdminFormFieldItem? Ok, string? Error)> HandleAsync(
        CreateFormFieldCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.CreateFormFieldAsync(command, ct);
}

public static class UpdateAdminFormField
{
    public static Task<(AdminFormFieldItem? Ok, string? Error)> HandleAsync(
        UpdateFormFieldCommand command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.UpdateFormFieldAsync(command, ct);
}

public static class DeactivateAdminFormField
{
    public sealed record Command(Guid TenantId, string ProcedureTypeCode, Guid FieldId);

    public static Task<bool> HandleAsync(
        Command command,
        IProceduresConfigAdminRepository repo,
        CancellationToken ct = default) =>
        repo.DeactivateFormFieldAsync(
            command.TenantId,
            command.ProcedureTypeCode,
            command.FieldId,
            ct);
}
