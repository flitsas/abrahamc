using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// audit.audit_log en memoria para tests/DEV (HU #9487).
/// </summary>
public sealed class InMemoryIdentityVerificationAuditRepository : IIdentityVerificationAuditRepository
{
    private readonly ConcurrentQueue<IdentityVerificationAuditEntry> _entries = new();

    public IReadOnlyList<IdentityVerificationAuditEntry> Entries => _entries.ToList();

    public Task AppendAsync(IdentityVerificationAuditEntry entry, CancellationToken ct)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
