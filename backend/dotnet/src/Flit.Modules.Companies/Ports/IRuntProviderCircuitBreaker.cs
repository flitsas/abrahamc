namespace Flit.Modules.Companies.Ports;

/// <summary>Circuit breaker por tenant+proveedor (#9447 AC2).</summary>
public interface IRuntProviderCircuitBreaker
{
    bool IsOpen(Guid tenantId, string providerCode, DateTimeOffset now);

    void RecordFailure(Guid tenantId, string providerCode, DateTimeOffset now);

    void RecordSuccess(Guid tenantId, string providerCode);
}
