namespace Flit.Modules.Companies.Domain;

/// <summary>Snapshot procedures.procedure_query_results tras consulta vehicular (#9447).</summary>
public sealed class ProcedureQueryResultSnapshot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProcedureInstanceId { get; private set; }
    public string QueryConnectorCode { get; private set; } = "RUNT";
    public string? EdgeRole { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string ResultJson { get; private set; } = "{}";
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public int RowVersion { get; private set; }

    private ProcedureQueryResultSnapshot() { }

    public static ProcedureQueryResultSnapshot CreateOk(
        Guid tenantId,
        Guid procedureInstanceId,
        string sourceProvider,
        string resultJson,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (procedureInstanceId == Guid.Empty)
            throw new ArgumentException("ProcedureInstanceId requerido", nameof(procedureInstanceId));

        return new ProcedureQueryResultSnapshot
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProcedureInstanceId = procedureInstanceId,
            QueryConnectorCode = "RUNT",
            EdgeRole = "vehiculo",
            Source = sourceProvider,
            Status = "ok",
            ResultJson = resultJson,
            RequestedAt = now,
            RespondedAt = now,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        };
    }
}
