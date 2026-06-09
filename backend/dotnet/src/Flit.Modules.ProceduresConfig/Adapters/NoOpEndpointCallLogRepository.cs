using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class NoOpEndpointCallLogRepository : IEndpointCallLogRepository
{
    public Task LogAsync(EndpointCallLogEntry entry, CancellationToken ct = default) =>
        Task.CompletedTask;
}
