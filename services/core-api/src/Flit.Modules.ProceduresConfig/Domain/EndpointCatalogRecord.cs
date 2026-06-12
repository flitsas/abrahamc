using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Registro de procedures_config.endpoint_catalog (RGL-03 #9439).</summary>
public sealed record EndpointCatalogRecord(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string Url,
    string Method,
    string AuthType,
    JsonElement AuthConfig,
    JsonElement FieldMapping,
    int TimeoutMs,
    bool IsActive,
    int RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
