using System.Text.Json;

namespace Flit.Modules.Procedures.Ports;

/// <summary>Snapshot por fuente en <c>procedures.procedure_query_results</c> (MTR-04 #9428).</summary>
public sealed record ProcedureQueryResultRecord(
    Guid Id,
    Guid TenantId,
    Guid ProcedureInstanceId,
    string QueryConnectorCode,
    string? EdgeRole,
    string Source,
    string Status,
    JsonElement Result,
    DateTimeOffset RequestedAt,
    DateTimeOffset? RespondedAt,
    Guid? IntegrationCallId,
    Guid CreatedBy,
    Guid UpdatedBy);

public interface IProcedureQueryResultRepository
{
    Task UpsertAsync(ProcedureQueryResultRecord record, CancellationToken ct = default);

    Task<IReadOnlyList<ProcedureQueryResultRecord>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
