using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;
using Flit.Modules.Procedures.Application;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Api.Services;

/// <summary>HU #10080 — ejecución async de consultas externas con job store en memoria.</summary>
public sealed class ProcedureQueryBackgroundRunner : IHostedService, IProcedureQueryJobStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryProcedureQueryJobStoreAdapter _store;

    public ProcedureQueryBackgroundRunner(
        IServiceScopeFactory scopeFactory,
        InMemoryProcedureQueryJobStoreAdapter store)
    {
        _scopeFactory = scopeFactory;
        _store = store;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ProcedureQueryJobStatus Enqueue(Guid procedureInstanceId, Guid tenantId) =>
        _store.Enqueue(procedureInstanceId, tenantId);

    public ProcedureQueryJobStatus? GetJob(Guid jobId) => _store.GetJob(jobId);

    public void MarkRunning(Guid jobId) => _store.MarkRunning(jobId);

    public void MarkCompleted(Guid jobId) => _store.MarkCompleted(jobId);

    public void MarkFailed(Guid jobId, string errorMessage) => _store.MarkFailed(jobId, errorMessage);

    public Guid EnqueueRunAsync(RunProcedureExternalQueries.Command command)
    {
        var job = _store.Enqueue(command.ProcedureInstanceId, command.TenantId);
        _ = ExecuteJobAsync(job.JobId, command);
        return job.JobId;
    }

    private async Task ExecuteJobAsync(Guid jobId, RunProcedureExternalQueries.Command command)
    {
        MarkRunning(jobId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var result = await RunProcedureExternalQueries.HandleAsync(
                command,
                sp.GetRequiredService<IProcedureInstanceRepository>(),
                sp.GetRequiredService<IProceduresConfigReadRepository>(),
                sp.GetRequiredService<IDocumentTypesReadRepository>(),
                sp.GetRequiredService<IExternalQueryProvider>(),
                sp.GetRequiredService<IExternalQueryCallLogRepository>(),
                sp.GetRequiredService<IRuntSyncLogRepository>(),
                sp.GetRequiredService<IProcedureQueryResultRepository>(),
                sp.GetRequiredService<ExternalQueryCircuitBreaker>(),
                CancellationToken.None);

            if (result.IsSuccess)
            {
                MarkCompleted(jobId);
            }
            else
            {
                MarkFailed(jobId, result.Error!.Message);
            }
        }
        catch (Exception ex)
        {
            MarkFailed(jobId, ex.Message);
        }
    }
}

/// <summary>Adaptador singleton sobre el store en memoria del módulo Procedures.</summary>
public sealed class InMemoryProcedureQueryJobStoreAdapter : IProcedureQueryJobStore
{
    private readonly Flit.Modules.Procedures.Adapters.InMemoryProcedureQueryJobStore _inner = new();

    public ProcedureQueryJobStatus Enqueue(Guid procedureInstanceId, Guid tenantId) =>
        _inner.Enqueue(procedureInstanceId, tenantId);

    public ProcedureQueryJobStatus? GetJob(Guid jobId) => _inner.GetJob(jobId);

    public void MarkRunning(Guid jobId) => _inner.MarkRunning(jobId);

    public void MarkCompleted(Guid jobId) => _inner.MarkCompleted(jobId);

    public void MarkFailed(Guid jobId, string errorMessage) => _inner.MarkFailed(jobId, errorMessage);
}
