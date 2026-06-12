using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryEndpointCatalogRepository : IEndpointCatalogRepository
{
    public static readonly Guid DemoEndpointId = Guid.Parse("00000000-0000-7000-8004-000000000001");
    public const string DemoEndpointCode = "ECHO_HEALTH";

    private readonly List<EndpointCatalogRecord> _entries =
    [
        new(
            DemoEndpointId,
            InMemoryProcedureRulesRepository.DemoTenantId,
            DemoEndpointCode,
            "Echo Health",
            "https://httpbin.org/get",
            "GET",
            "none",
            JsonRuleElements.Parse("{}"),
            JsonRuleElements.Parse("{}"),
            TimeoutMs: 5000,
            IsActive: true,
            RowVersion: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow),
    ];

    public Task<IReadOnlyList<EndpointCatalogRecord>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _entries
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Code)
            .ToList();
        return Task.FromResult<IReadOnlyList<EndpointCatalogRecord>>(list);
    }

    public Task<EndpointCatalogRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var item = _entries.FirstOrDefault(e => e.TenantId == tenantId && e.Id == id);
        return Task.FromResult(item);
    }

    public Task<EndpointCatalogRecord?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        var item = _entries.FirstOrDefault(e =>
            e.TenantId == tenantId &&
            string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task<EndpointCatalogRecord> AddAsync(EndpointCatalogWriteModel model, CancellationToken ct = default)
    {
        if (_entries.Any(e =>
                e.TenantId == model.TenantId &&
                string.Equals(e.Code, model.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("duplicate key: uq_endpoint_catalog_tenant_code");
        }

        using var authDoc = JsonDocument.Parse(model.AuthConfigJson);
        using var mappingDoc = JsonDocument.Parse(model.FieldMappingJson);
        var record = new EndpointCatalogRecord(
            Guid.NewGuid(),
            model.TenantId,
            model.Code,
            model.Name,
            model.Url,
            model.Method,
            model.AuthType,
            authDoc.RootElement.Clone(),
            mappingDoc.RootElement.Clone(),
            model.TimeoutMs,
            model.IsActive,
            RowVersion: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        _entries.Add(record);
        return Task.FromResult(record);
    }

    public Task<EndpointCatalogRecord?> UpdateAsync(EndpointCatalogWriteModel model, CancellationToken ct = default)
    {
        var index = _entries.FindIndex(e => e.TenantId == model.TenantId && e.Id == model.Id);
        if (index < 0)
        {
            return Task.FromResult<EndpointCatalogRecord?>(null);
        }

        var current = _entries[index];
        if (model.ExpectedRowVersion is not null && current.RowVersion != model.ExpectedRowVersion)
        {
            return Task.FromResult<EndpointCatalogRecord?>(null);
        }

        using var authDoc = JsonDocument.Parse(model.AuthConfigJson);
        using var mappingDoc = JsonDocument.Parse(model.FieldMappingJson);
        var updated = current with
        {
            Name = model.Name,
            Url = model.Url,
            Method = model.Method,
            AuthType = model.AuthType,
            AuthConfig = authDoc.RootElement.Clone(),
            FieldMapping = mappingDoc.RootElement.Clone(),
            TimeoutMs = model.TimeoutMs,
            IsActive = model.IsActive,
            RowVersion = current.RowVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _entries[index] = updated;
        return Task.FromResult<EndpointCatalogRecord?>(updated);
    }

    public Task<bool> SoftDeleteAsync(Guid tenantId, Guid id, Guid deletedBy, CancellationToken ct = default)
    {
        var removed = _entries.RemoveAll(e => e.TenantId == tenantId && e.Id == id);
        return Task.FromResult(removed > 0);
    }
}
