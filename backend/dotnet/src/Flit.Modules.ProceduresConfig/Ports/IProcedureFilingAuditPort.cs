namespace Flit.Modules.ProceduresConfig.Ports;

public interface IProcedureFilingAuditPort
{
    Task RecordOmittedQueriesAsync(
        Guid tenantId,
        Guid filedByUserId,
        string procedureTypeCode,
        IReadOnlyList<string> omittedQueries,
        CancellationToken ct = default);
}
