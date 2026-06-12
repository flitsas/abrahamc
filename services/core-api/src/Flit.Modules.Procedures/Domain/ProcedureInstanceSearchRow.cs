namespace Flit.Modules.Procedures.Domain;

/// <summary>Fila de grilla paginada (#10079).</summary>
public sealed record ProcedureInstanceSearchRow(
    Guid Id,
    Guid TenantId,
    string ReferenceNumber,
    string CompositeId,
    string ProcedureTypeCode,
    string State,
    Guid ProcedureTypeId,
    Guid? TrafficAgencyId,
    DateTimeOffset? RadicatedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed record ProcedureInstanceSearchResult(
    IReadOnlyList<ProcedureInstanceSearchRow> Items,
    int TotalCount);

public sealed record ProcedureReferenceContext(
    string TypeCode,
    string TenantCode,
    string OtCode);
