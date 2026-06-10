using System.Collections.Concurrent;
using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class InMemoryProcedureFieldValueRepository : IProcedureFieldValueRepository
{
    private readonly ConcurrentBag<ProcedureFieldValueEntry> _entries = [];

    public Task SaveManyAsync(IReadOnlyList<ProcedureFieldValueEntry> entries, CancellationToken ct = default)
    {
        foreach (var entry in entries)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<ProcedureFieldValueEntry> GetAll() => _entries.ToList();
}
