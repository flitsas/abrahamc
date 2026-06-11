namespace Flit.Modules.Companies.Ports;

public sealed record TenantUserExceptionRow(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string? Reason,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record TenantAuthorizedTrafficAgencyRow(
    Guid Id,
    Guid TenantId,
    Guid TrafficAgencyId,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);

public interface ITenantGovernanceRepository
{
    Task<IReadOnlyList<TenantUserExceptionRow>> ListUserExceptionsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<TenantUserExceptionRow?> GetUserExceptionByIdAsync(
        Guid tenantId,
        Guid exceptionId,
        CancellationToken ct = default);

    Task<Guid> CreateUserExceptionAsync(
        Guid tenantId,
        Guid userId,
        string? reason,
        DateTimeOffset? expiresAt,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<bool> SoftDeleteUserExceptionAsync(
        Guid tenantId,
        Guid exceptionId,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<bool> UserBelongsToTenantAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> IsUserExemptFromOnlyOwnAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<TenantAuthorizedTrafficAgencyRow>> ListAuthorizedTrafficAgenciesAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task UpsertAuthorizedTrafficAgencyAsync(
        Guid tenantId,
        Guid trafficAgencyId,
        bool isEnabled,
        Guid actorUserId,
        CancellationToken ct = default);

    /// <summary>Null cuando no hay fila en la matriz (autorizado por defecto).</summary>
    Task<bool?> GetTrafficAgencyEnabledAsync(
        Guid tenantId,
        Guid trafficAgencyId,
        CancellationToken ct = default);
}
