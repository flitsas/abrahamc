using Flit.Modules.Procedures.Domain;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU #10079 — listado paginado de instancias con ID compuesto.</summary>
public static class ListProcedureInstances
{
    public sealed record Query(
        Guid TenantId,
        int Page = 1,
        int PageSize = 20,
        string? State = null,
        string? ProcedureTypeCode = null);

    public sealed record ListItem(
        Guid Id,
        string CompositeId,
        string ReferenceNumber,
        string ProcedureTypeCode,
        string State,
        Guid ProcedureTypeId,
        Guid? TrafficAgencyId,
        DateTimeOffset? RadicatedAt,
        DateTimeOffset CreatedAt,
        Guid CreatedBy);

    public sealed record Response(
        IReadOnlyList<ListItem> Items,
        int TotalCount,
        int Page,
        int PageSize);

    public static async Task<Response> HandleAsync(
        Query query,
        IProcedureInstanceRepository repository,
        CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var result = await repository.SearchAsync(
            query.TenantId,
            page,
            pageSize,
            query.State,
            query.ProcedureTypeCode,
            ct);

        var items = result.Items.Select(row => new ListItem(
            row.Id,
            row.CompositeId,
            row.ReferenceNumber,
            row.ProcedureTypeCode,
            row.State,
            row.ProcedureTypeId,
            row.TrafficAgencyId,
            row.RadicatedAt,
            row.CreatedAt,
            row.CreatedBy)).ToList();

        return new Response(items, result.TotalCount, page, pageSize);
    }
}
