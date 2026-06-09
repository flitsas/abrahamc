using System.Collections.Concurrent;
using Flit.Modules.Integrations.Ports;

namespace Flit.Modules.Integrations.Adapters;

public sealed class InMemoryRuntSyncLogRepository : IRuntSyncLogRepository
{
    private readonly ConcurrentBag<RuntSyncLogEntry> _entries = [];

    public Task LogAsync(RuntSyncLogEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public IReadOnlyList<RuntSyncLogEntry> GetAllEntries() => _entries.ToList();
}
