namespace Flit.Modules.Procedures.Ports;

/// <summary>Estado de job async de consultas externas (#10080).</summary>
public sealed record ProcedureQueryJobStatus(
    Guid JobId,
    Guid ProcedureInstanceId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage);

public interface IProcedureQueryJobStore
{
    ProcedureQueryJobStatus Enqueue(Guid procedureInstanceId, Guid tenantId);

    ProcedureQueryJobStatus? GetJob(Guid jobId);

    void MarkRunning(Guid jobId);

    void MarkCompleted(Guid jobId);

    void MarkFailed(Guid jobId, string errorMessage);
}
