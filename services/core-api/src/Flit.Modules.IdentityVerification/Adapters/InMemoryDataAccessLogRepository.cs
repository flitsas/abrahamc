using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// audit.data_access_log en memoria para tests/DEV (HU #9490 AC2).
/// </summary>
public sealed class InMemoryDataAccessLogRepository : IDataAccessLogRepository
{
    private readonly ConcurrentQueue<DataAccessLogEntry> _entries = new();

    public IReadOnlyList<DataAccessLogEntry> Entries => _entries.ToList();

    public Task AppendAsync(DataAccessLogEntry entry, CancellationToken ct)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
