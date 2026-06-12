using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Ports;

public sealed record RuleExecutionLogEntry(
    Guid TenantId,
    Guid ProcedureTypeId,
    Guid? ProcedureInstanceId,
    JsonElement Payload,
    JsonElement MatchedRules,
    JsonElement Result,
    DateTimeOffset EvaluatedAt);

public interface IRuleExecutionLogRepository
{
    Task LogAsync(RuleExecutionLogEntry entry, CancellationToken ct = default);
}
