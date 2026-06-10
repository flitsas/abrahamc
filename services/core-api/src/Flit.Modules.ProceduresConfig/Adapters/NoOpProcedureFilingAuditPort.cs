using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class NoOpProcedureFilingAuditPort : IProcedureFilingAuditPort
{
    public Task RecordOmittedQueriesAsync(
        Guid tenantId,
        Guid filedByUserId,
        string procedureTypeCode,
        IReadOnlyList<string> omittedQueries,
        CancellationToken ct = default) => Task.CompletedTask;
}
