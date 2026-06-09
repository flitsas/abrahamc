using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryEndpointCallLogRepository : IEndpointCallLogRepository
{
    private readonly List<EndpointCallLogEntry> _entries = [];

    public IReadOnlyList<EndpointCallLogEntry> Entries => _entries;

    public Task LogAsync(EndpointCallLogEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }
}
