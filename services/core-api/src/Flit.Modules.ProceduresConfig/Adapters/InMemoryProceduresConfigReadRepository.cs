using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

/// <summary>Config de demo TRA_ESTANDAR para DEV sin PostgreSQL (HU #9426) + admin matriz.</summary>
public sealed class InMemoryProceduresConfigReadRepository
    : IProceduresConfigReadRepository,
      IProceduresConfigAdminRepository
{
    public static readonly Guid DemoTenantId = Guid.Parse("01930101-0001-7001-8001-000000000001");
    public static readonly Guid DemoOtBogotaId = Guid.Parse("01930102-0001-7001-8001-000000000101");
    public static readonly Guid DemoTypeTraspasoId = Guid.Parse("00000000-0000-7000-8002-000000000010");
    public static readonly Guid DemoTypeLeasingId = Guid.Parse("00000000-0000-7000-8002-000000000011");
    public static readonly Guid DemoTypeInactiveId = Guid.Parse("00000000-0000-7000-8002-000000000099");
    public static readonly Guid LeasingTenantId = Guid.Parse("01930101-0001-7001-8001-000000000099");

    private static readonly ProcedureEdgeConfig[] TraspasoEdges =
    [
        new("vehiculo", "Vehículo", "vehicle", true, true, 1, "Vehículo"),
        new("propietario", "Propietario / Vendedor", "person", true, true, 2, "Vendedor"),
        new("comprador", "Comprador", "person", true, true, 3, "Comprador"),
    ];

    private static readonly ProcedureRequiredDocumentConfig[] TraspasoRequiredDocuments =
    [
        new("CC", "Cédula de ciudadanía", "propietario", "upload", true, 1, "propietario"),
        new("CC", "Cédula de ciudadanía", "comprador", "upload", true, 2, "comprador"),
    ];

    private static readonly ProcedureFormSectionConfig[] TraspasoSections =
    [
        new("vehiculo_datos", "Datos del vehículo", 1, "interactive", "vehiculo",
        [
            new("placa", "text", "Placa", true, 1, "lleno", true, new Dictionary<string, object?>(), []),
            new("vin", "text", "VIN", false, 2, "vacio", false, new Dictionary<string, object?>(), []),
        ]),
        new("propietario_datos", "Propietario / Vendedor", 2, "interactive", "propietario",
        [
            new("doc_propietario", "text", "Documento propietario", true, 1, "lleno", true,
                new Dictionary<string, object?>(), []),
        ]),
        new("comprador_datos", "Comprador", 3, "interactive", "comprador",
        [
            new("doc_comprador", "text", "Documento comprador", true, 1, "lleno", true,
                new Dictionary<string, object?>(), []),
        ]),
    ];

    private static readonly ProcedureQueryConfig[] TraspasoQueries =
    [
        new("RUNT", "vehiculo", true, false, "any", 1),
        new("RUNT", "propietario", true, false, "natural", 1),
        new("SIMIT", "propietario", true, false, "natural", 2),
        new("SIMIT", "propietario", true, false, "juridica", 4),
        new("RUES", "propietario", true, false, "juridica", 3),
        new("SIMIT", "comprador", true, false, "natural", 2),
        new("RUES", "comprador", true, false, "juridica", 3),
    ];

    private static readonly ProcedureEdgeConfig[] CambioColorEdges =
    [
        new("vehiculo", "Vehículo", "vehicle", true, true, 1, "Vehículo"),
        new("propietario", "Propietario", "person", true, true, 2, "Propietario"),
    ];

    private static readonly ProcedureQueryConfig[] CambioColorQueries =
    [
        new("RUNT", "vehiculo", true, false, "any", 1),
        new("RUNT", "propietario", true, false, "natural", 1),
    ];

    private static readonly ProcedureEdgeConfig[] LeasingEdges =
    [
        new("vehiculo", "Vehículo", "vehicle", true, true, 1, "Vehículo"),
        new("propietario", "Propietario", "person", true, true, 2, "Propietario"),
        new("locatario", "Locatario", "person", true, true, 3, "Locatario"),
    ];

    private static readonly ProcedureQueryConfig[] LeasingQueries =
    [
        new("SIMIT", "locatario", true, false, "any", 1),
        new("FIRMA", "locatario", false, true, "any", 2),
        new("RTM", "locatario", false, true, "any", 3),
    ];

    public static readonly Guid DemoTypeCambioColorId = Guid.Parse("00000000-0000-7000-8002-000000000020");

    private readonly Dictionary<(Guid TenantId, string Code), ProcedureConfigurationBundle> _bundles = new()
    {
        [(DemoTenantId, "TRA_ESTANDAR")] = BuildTraspasoBundle(),
        [(DemoTenantId, "CAMBIO_COLOR")] = BuildCambioColorBundle(),
        [(LeasingTenantId, "MAT_LEASING")] = BuildLeasingBundle(),
    };

    private readonly List<(Guid TenantId, Guid? OtId, ProcedureTypeListItem Item, bool ActivationActive)> _activations =
    [
        (DemoTenantId, DemoOtBogotaId,
            new ProcedureTypeListItem(DemoTypeTraspasoId, "TRA_ESTANDAR", "traspaso-estandar", "Traspaso Estándar",
                "TRASPASO", "Traspaso", 3),
            true),
        (DemoTenantId, DemoOtBogotaId,
            new ProcedureTypeListItem(DemoTypeCambioColorId, "CAMBIO_COLOR", "cambio-color", "Cambio de Color",
                "OTROS_TRAMITES", "Otros trámites", 2),
            true),
        (DemoTenantId, DemoOtBogotaId,
            new ProcedureTypeListItem(DemoTypeLeasingId, "MAT_LEASING", "matricula-leasing", "Matrícula Leasing",
                "MATRICULAS", "Matrículas", 2),
            false),
        (LeasingTenantId, DemoOtBogotaId,
            new ProcedureTypeListItem(DemoTypeLeasingId, "MAT_LEASING", "matricula-leasing", "Matrícula Leasing",
                "MATRICULAS", "Matrículas", 2),
            true),
    ];

    private static ProcedureConfigurationBundle BuildTraspasoBundle() =>
        new(
            DemoTypeTraspasoId,
            "TRA_ESTANDAR",
            "traspaso-estandar",
            "Traspaso Estándar",
            "TRASPASO",
            3,
            GlobalIsActive: true,
            TenantActivation: new ProcedureActivationLayer(
                DemoOtBogotaId,
                """{"edges":{"comprador":{"roleLabel":"Comprador (tenant)"}}}"""),
            OtActivation: new ProcedureActivationLayer(
                DemoOtBogotaId,
                """{"queries":[{"connector":"SIMIT","edge":"comprador","personKind":"natural","isMandatory":false}]}"""),
            TraspasoEdges,
            TraspasoSections,
            TraspasoQueries,
            TraspasoRequiredDocuments);

    private static ProcedureConfigurationBundle BuildCambioColorBundle() =>
        new(
            DemoTypeCambioColorId,
            "CAMBIO_COLOR",
            "cambio-color",
            "Cambio de Color",
            "OTROS_TRAMITES",
            2,
            GlobalIsActive: true,
            TenantActivation: new ProcedureActivationLayer(DemoOtBogotaId, "{}"),
            OtActivation: null,
            CambioColorEdges,
            [],
            CambioColorQueries,
            []);

    private static ProcedureConfigurationBundle BuildLeasingBundle() =>
        new(
            DemoTypeLeasingId,
            "MAT_LEASING",
            "matricula-leasing",
            "Matrícula Leasing",
            "MATRICULAS",
            3,
            GlobalIsActive: true,
            TenantActivation: new ProcedureActivationLayer(
                DemoOtBogotaId,
                """{"queries":[{"connector":"SIMIT","edge":"locatario","personKind":"any","isMandatory":false}]}"""),
            OtActivation: null,
            LeasingEdges,
            [],
            LeasingQueries,
            []);

    public Task<IReadOnlyList<ProcedureTypeListItem>> ListEffectiveActiveTypesAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var items = _activations
            .Where(a => a.TenantId == tenantId && a.ActivationActive)
            .Where(a => trafficAgencyId is null || a.OtId == trafficAgencyId)
            .Select(a => a.Item)
            .DistinctBy(i => i.Code)
            .OrderBy(i => i.FamilyName)
            .ThenBy(i => i.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<ProcedureTypeListItem>>(items);
    }

    public Task<ProcedureConfigurationBundle?> LoadConfigurationBundleAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        if (!_bundles.TryGetValue((tenantId, procedureTypeCode), out var bundle))
        {
            return Task.FromResult<ProcedureConfigurationBundle?>(null);
        }

        if (procedureTypeCode.Equals("INACTIVE_GLOBAL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<ProcedureConfigurationBundle?>(
                bundle with { GlobalIsActive = false });
        }

        if (!_activations.Any(a =>
                a.TenantId == tenantId &&
                a.Item.Code.Equals(procedureTypeCode, StringComparison.OrdinalIgnoreCase) &&
                a.ActivationActive &&
                (trafficAgencyId is null || a.OtId == trafficAgencyId)))
        {
            return Task.FromResult<ProcedureConfigurationBundle?>(null);
        }

        return Task.FromResult<ProcedureConfigurationBundle?>(bundle);
    }

    public Task<IReadOnlyList<AdminProcedureTypeSummary>> ListTypesForAdminAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var items = _bundles.Keys
            .Where(k => k.TenantId == tenantId)
            .Select(k =>
            {
                var bundle = _bundles[k];
                var activationActive = _activations.Any(a =>
                    a.TenantId == tenantId &&
                    a.Item.Code.Equals(k.Code, StringComparison.OrdinalIgnoreCase) &&
                    a.ActivationActive &&
                    (trafficAgencyId is null || a.OtId == trafficAgencyId));

                return new AdminProcedureTypeSummary(
                    bundle.ProcedureTypeId,
                    bundle.Code,
                    bundle.Name,
                    bundle.FamilyCode,
                    bundle.GlobalIsActive,
                    activationActive,
                    bundle.Edges.Count(e => e.IsActive),
                    bundle.MaxSteps);
            })
            .OrderBy(i => i.FamilyCode)
            .ThenBy(i => i.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<AdminProcedureTypeSummary>>(items);
    }

    public Task<AdminProcedureMatrixView?> GetMatrixAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        if (!_bundles.TryGetValue((tenantId, procedureTypeCode), out var bundle))
        {
            return Task.FromResult<AdminProcedureMatrixView?>(null);
        }

        var activationActive = _activations.Any(a =>
            a.TenantId == tenantId
            && a.Item.Code == procedureTypeCode
            && a.ActivationActive
            && (trafficAgencyId is null || a.OtId == trafficAgencyId));

        var view = new AdminProcedureMatrixView(
            bundle.ProcedureTypeId,
            bundle.Code,
            bundle.Name,
            bundle.FamilyCode,
            bundle.MaxSteps,
            bundle.GlobalIsActive,
            activationActive,
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

        return Task.FromResult<AdminProcedureMatrixView?>(view);
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
        CancellationToken ct = default)
    {
        if (!_bundles.TryGetValue((tenantId, procedureTypeCode), out var bundle))
        {
            return Task.FromResult(false);
        }

        var edgeIndex = bundle.Edges.ToList().FindIndex(e =>
            e.Code.Equals(edgeCode, StringComparison.OrdinalIgnoreCase));
        if (edgeIndex < 0)
        {
            return Task.FromResult(false);
        }

        var edges = bundle.Edges.ToList();
        var edge = edges[edgeIndex];
        edges[edgeIndex] = edge with { IsActive = isActive };
        _bundles[(tenantId, procedureTypeCode)] = bundle with { Edges = edges };

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<AdminCatalogFamily>> ListCatalogFamiliesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogFamily>>([
            new(Guid.Parse("00000000-0000-7000-8002-000000000001"), "TRASPASO", "Traspaso", 2),
        ]);

    public Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogEdgesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogEdge>>(TraspasoEdges.Select(e =>
            new AdminCatalogEdge(e.Code, e.Name, e.EdgeKind, e.DisplayOrder)).ToList());

    public Task<(CreateProcedureTypeResult? Ok, CreateProcedureTypeError? Error)> CreateProcedureTypeAsync(
        CreateProcedureTypeCommand command,
        CancellationToken ct = default) =>
        Task.FromResult<(CreateProcedureTypeResult?, CreateProcedureTypeError?)>((
            null,
            new CreateProcedureTypeError(
                CreateProcedureTypeErrorKind.Validation,
                "Crear tipos requiere PostgreSQL (ConnectionStrings:Core).")));

    public Task<IReadOnlyList<AdminCatalogDocumentType>> ListCatalogDocumentTypesAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AdminCatalogDocumentType>>([
            new("CC", "Cédula de ciudadanía", "natural", 1),
        ]);

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
