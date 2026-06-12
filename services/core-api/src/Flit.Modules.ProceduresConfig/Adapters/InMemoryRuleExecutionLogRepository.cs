using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryRuleExecutionLogRepository : IRuleExecutionLogRepository
{
    private readonly List<RuleExecutionLogEntry> _entries = [];

    public IReadOnlyList<RuleExecutionLogEntry> Entries => _entries;

    public Task LogAsync(RuleExecutionLogEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }
}
