namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryProcedureFilingAuditPort : Ports.IProcedureFilingAuditPort
{
    public sealed record AuditEntry(
        Guid TenantId,
        Guid FiledByUserId,
        string ProcedureTypeCode,
        IReadOnlyList<string> OmittedQueries,
        DateTimeOffset RecordedAt);

    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries => _entries;

    public Task RecordOmittedQueriesAsync(
        Guid tenantId,
        Guid filedByUserId,
        string procedureTypeCode,
        IReadOnlyList<string> omittedQueries,
        CancellationToken ct = default)
    {
        _entries.Add(new AuditEntry(tenantId, filedByUserId, procedureTypeCode, omittedQueries, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
}
