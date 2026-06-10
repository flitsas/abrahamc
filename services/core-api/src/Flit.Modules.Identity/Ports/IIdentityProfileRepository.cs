using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Perfil autoservicio y cambio de contraseña (prereq HU #9420).</summary>
public interface IIdentityProfileRepository
{
    Task ApplySessionGucAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default);

    Task<ProfileRow?> GetProfileAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> UpdateProfileAsync(
        Guid tenantId,
        Guid userId,
        string? fullName,
        string? phone,
        string? locale,
        Guid updatedBy,
        CancellationToken ct = default);

    Task<bool> UpdatePasswordAsync(
        Guid userId,
        string passwordHash,
        DateTimeOffset at,
        CancellationToken ct = default);
}
