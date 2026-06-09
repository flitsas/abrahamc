using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU MTR-04 #9428 — lectura de snapshots persistidos por instancia.</summary>
public static class GetProcedureQueryResults
{
    public sealed record Query(Guid ProcedureInstanceId, Guid TenantId);

    public sealed record QueryResultItem(
        string ConnectorCode,
        string? EdgeRole,
        string Status,
        bool Succeeded,
        bool CircuitOpen,
        bool Mandatory,
        Guid? IntegrationCallId,
        Guid? SnapshotId);

    public sealed record Response(
        Guid ProcedureInstanceId,
        IReadOnlyList<QueryResultItem> Results,
        bool CanContinue);

    public enum ErrorKind
    {
        InstanceNotFound,
    }

    public sealed record QueryError(ErrorKind Kind, string Message);

    public static async Task<Result<Response, QueryError>> HandleAsync(
        Query query,
        IProcedureInstanceRepository instanceRepo,
        IProcedureQueryResultRepository queryResultRepo,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(query.ProcedureInstanceId, query.TenantId, ct);
        if (instance is null)
        {
            return Result<Response, QueryError>.Failure(
                new QueryError(ErrorKind.InstanceNotFound, "Instancia de trámite no encontrada."));
        }

        var records = await queryResultRepo.ListByInstanceAsync(query.TenantId, query.ProcedureInstanceId, ct);
        var items = records.Select(r =>
        {
            var succeeded = string.Equals(r.Status, "ok", StringComparison.OrdinalIgnoreCase);
            return new QueryResultItem(
                r.QueryConnectorCode,
                r.EdgeRole,
                r.Status,
                succeeded,
                CircuitOpen: false,
                Mandatory: false,
                r.IntegrationCallId,
                r.Id);
        }).ToList();

        var canContinue = !string.Equals(instance.State, ProcedureStates.Borrador, StringComparison.OrdinalIgnoreCase)
            || items.All(i => i.Succeeded || !i.Mandatory);

        return Result<Response, QueryError>.Success(
            new Response(query.ProcedureInstanceId, items, canContinue));
    }
}
