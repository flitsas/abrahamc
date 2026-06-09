using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

/// <summary>Puerto de persistencia para integrations.webhook_events (HU #9459 INT-02).</summary>
public interface IWebhookEventRepository
{
    /// <summary>
    /// Verifica si ya existe un evento con la misma idempotency_key para el tenant.
    /// Usado por AC1 para detectar duplicados y responder 200 sin reejecutar efectos.
    /// </summary>
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>Persiste un nuevo webhook event (AC2).</summary>
    Task AddAsync(WebhookEvent entry, CancellationToken ct = default);
}
