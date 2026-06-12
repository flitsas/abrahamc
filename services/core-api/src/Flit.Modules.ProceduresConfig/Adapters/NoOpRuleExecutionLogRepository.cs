using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class NoOpRuleExecutionLogRepository : IRuleExecutionLogRepository
{
    public Task LogAsync(RuleExecutionLogEntry entry, CancellationToken ct = default) =>
        Task.CompletedTask;
}
