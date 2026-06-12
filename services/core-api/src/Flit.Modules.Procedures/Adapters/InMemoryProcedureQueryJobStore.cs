using System.Collections.Concurrent;
using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class InMemoryProcedureQueryJobStore : IProcedureQueryJobStore
{
    private readonly ConcurrentDictionary<Guid, ProcedureQueryJobStatus> _jobs = new();

    public ProcedureQueryJobStatus Enqueue(Guid procedureInstanceId, Guid tenantId)
    {
        var jobId = Guid.NewGuid();
        var status = new ProcedureQueryJobStatus(
            jobId,
            procedureInstanceId,
            "pending",
            DateTimeOffset.UtcNow,
            null,
            null);
        _jobs[jobId] = status;
        return status;
    }

    public ProcedureQueryJobStatus? GetJob(Guid jobId) =>
        _jobs.TryGetValue(jobId, out var status) ? status : null;

    public void MarkRunning(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var current))
        {
            _jobs[jobId] = current with { Status = "running" };
        }
    }

    public void MarkCompleted(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var current))
        {
            _jobs[jobId] = current with { Status = "completed", CompletedAt = DateTimeOffset.UtcNow };
        }
    }

    public void MarkFailed(Guid jobId, string errorMessage)
    {
        if (_jobs.TryGetValue(jobId, out var current))
        {
            _jobs[jobId] = current with
            {
                Status = "failed",
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = errorMessage,
            };
        }
    }
}
