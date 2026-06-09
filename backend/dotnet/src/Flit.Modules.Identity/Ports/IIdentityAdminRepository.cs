using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Consolas Super Admin / Tenant Admin (HU #9419).</summary>
public interface IIdentityAdminRepository
{
    Task ApplySessionGucAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default);

    Task<IReadOnlyList<TenantRow>> ListTenantsAsync(
        string? status,
        string? search,
        CancellationToken ct = default);

    Task<TenantRow?> GetTenantByIdAsync(Guid id, CancellationToken ct = default);

    Task<string?> GetTenantNameAsync(Guid id, CancellationToken ct = default);

    Task<TenantRow> CreateTenantAsync(
        string name,
        string nit,
        string slug,
        string? settingsJson,
        Guid createdBy,
        CancellationToken ct = default);

    Task<TenantRow?> UpdateTenantAsync(
        Guid id,
        string? name,
        string? status,
        string? settingsJson,
        Guid updatedBy,
        CancellationToken ct = default);

    Task<bool> TenantSlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct = default);

    Task<bool> TenantNitExistsAsync(string nit, Guid? excludeId, CancellationToken ct = default);

    Task<bool> EmailExistsGloballyAsync(string email, CancellationToken ct = default);

    Task<(IReadOnlyList<CollaboratorRow> Items, int Total)> ListCollaboratorsAsync(
        Guid tenantId,
        int page,
        int limit,
        string? search,
        CancellationToken ct = default);

    Task<CollaboratorRow?> GetCollaboratorAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);

    Task<Guid> CreateCollaboratorUserAsync(
        Guid tenantId,
        string email,
        Guid createdBy,
        CancellationToken ct = default);

    Task CreateCollaboratorProfileAsync(
        Guid tenantId,
        Guid userId,
        string fullName,
        string? phone,
        Guid createdBy,
        CancellationToken ct = default);

    Task<bool> UpdateCollaboratorAsync(
        Guid tenantId,
        Guid userId,
        string? fullName,
        string? phone,
        string? accountState,
        Guid updatedBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<AssignableRoleRow>> ListAssignableRolesAsync(
        bool excludeSuperAdmin,
        CancellationToken ct = default);

    Task<AssignableRoleRow?> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default);

    Task EnsureUserRoleAsync(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid actorId,
        CancellationToken ct = default);

    Task<bool> RemoveUserRoleAsync(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid actorId,
        CancellationToken ct = default);
}
