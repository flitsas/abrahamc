namespace Flit.Modules.ProceduresConfig.Domain;

public sealed record ProcedureTypeListItem(
    Guid Id,
    string Code,
    string Slug,
    string Name,
    string FamilyCode,
    string FamilyName,
    int MaxSteps);

public sealed record ProcedureEdgeConfig(
    string Code,
    string Name,
    string EdgeKind,
    bool IsActive,
    bool IsRequired,
    int DisplayOrder,
    string? RoleLabel);

public sealed record ProcedureFormFieldConfig(
    string FieldKey,
    string DataType,
    string Label,
    bool IsRequired,
    int DisplayOrder,
    string UiState,
    bool IsTrigger,
    IReadOnlyDictionary<string, object?> Validation,
    IReadOnlyList<object> Options);

public sealed record ProcedureFormSectionConfig(
    string SectionKey,
    string Title,
    int DisplayOrder,
    string UiMode,
    string? EdgeCode,
    IReadOnlyList<ProcedureFormFieldConfig> Fields);

public sealed record ProcedureQueryConfig(
    string ConnectorCode,
    string? EdgeCode,
    bool IsMandatory,
    bool IsOmitible,
    string PersonKindFilter,
    int DisplayOrder);

public sealed record ProcedureRequiredDocumentConfig(
    string DocumentTypeCode,
    string DocumentTypeName,
    string? EdgeCode,
    string Kind,
    bool IsRequired,
    int DisplayOrder,
    string? ActorRole);

public sealed record ResolvedProcedureConfiguration(
    Guid ProcedureTypeId,
    string Code,
    string Slug,
    string Name,
    string FamilyCode,
    int MaxSteps,
    /// <summary>Capa efectiva de gobernanza: global, company u ot (HU #9695).</summary>
    string Scope,
    IReadOnlyList<string> ResolutionLayers,
    IReadOnlyList<ProcedureEdgeConfig> Edges,
    IReadOnlyList<ProcedureFormSectionConfig> Sections,
    IReadOnlyList<ProcedureQueryConfig> Queries,
    IReadOnlyList<ProcedureRequiredDocumentConfig> RequiredDocuments);

public sealed record ProcedureActivationLayer(
    Guid? TrafficAgencyId,
    string OverridesJson);

public sealed record ProcedureConfigurationBundle(
    Guid ProcedureTypeId,
    string Code,
    string Slug,
    string Name,
    string FamilyCode,
    int MaxSteps,
    bool GlobalIsActive,
    ProcedureActivationLayer? TenantActivation,
    ProcedureActivationLayer? OtActivation,
    IReadOnlyList<ProcedureEdgeConfig> Edges,
    IReadOnlyList<ProcedureFormSectionConfig> Sections,
    IReadOnlyList<ProcedureQueryConfig> Queries,
    IReadOnlyList<ProcedureRequiredDocumentConfig> RequiredDocuments);
