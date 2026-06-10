using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Ports;

public sealed record EndpointCallLogEntry(
    Guid TenantId,
    string EndpointCode,
    Guid? ProcedureInstanceId,
    JsonElement Request,
    JsonElement? Response,
    int? HttpStatus,
    bool Succeeded,
    DateTimeOffset CalledAt);

public interface IEndpointCallLogRepository
{
    Task LogAsync(EndpointCallLogEntry entry, CancellationToken ct = default);
}
