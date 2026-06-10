using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF Core para integrations.webhook_events (HU #9459 INT-02).
/// La unicidad de idempotency_key es garantizada por UNIQUE en DB y verificada antes de insertar.
/// </summary>
public sealed class EfWebhookEventRepository(FlitDbContext db) : IWebhookEventRepository
{
    public async Task<bool> ExistsByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken ct = default)
    {
        return await db.WebhookEvents
            .AnyAsync(e => e.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task AddAsync(WebhookEvent entry, CancellationToken ct = default)
    {
        await db.WebhookEvents.AddAsync(entry, ct);
    }
}
