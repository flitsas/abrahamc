using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public interface IEndpointCatalogRepository
{
    Task<IReadOnlyList<EndpointCatalogRecord>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<EndpointCatalogRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<EndpointCatalogRecord?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);

    Task<EndpointCatalogRecord> AddAsync(EndpointCatalogWriteModel model, CancellationToken ct = default);

    Task<EndpointCatalogRecord?> UpdateAsync(EndpointCatalogWriteModel model, CancellationToken ct = default);

    Task<bool> SoftDeleteAsync(Guid tenantId, Guid id, Guid deletedBy, CancellationToken ct = default);
}

public sealed record EndpointCatalogWriteModel(
    Guid? Id,
    Guid TenantId,
    string Code,
    string Name,
    string Url,
    string Method,
    string AuthType,
    string AuthConfigJson,
    string FieldMappingJson,
    int TimeoutMs,
    bool IsActive,
    Guid ActorUserId,
    int? ExpectedRowVersion = null);
