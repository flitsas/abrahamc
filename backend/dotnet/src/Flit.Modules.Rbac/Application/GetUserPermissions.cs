using Flit.Modules.Rbac.Ports;

namespace Flit.Modules.Rbac.Application;

/// <summary>
/// Devuelve set plano de permission codes efectivos del usuario.
/// Endpoint: GET /api/v1/permissions/me.
/// Usa cache (Redis TTL 5min) para evitar consulta a BD en cada request.
/// </summary>
public static class GetUserPermissions
{
    public sealed record Query(Guid UserId);

    public sealed record Response(IReadOnlyList<string> Permissions);

    public static async Task<Response> HandleAsync(
        Query query,
        IPermissionsRepository permissionsRepo,
        IPermissionsCache cache,
        CancellationToken ct = default)
    {
        var cached = await cache.GetAsync(query.UserId, ct);
        if (cached is not null)
            return new Response(cached);

        var codes = await permissionsRepo.GetUserPermissionCodesAsync(query.UserId, ct);
        await cache.SetAsync(query.UserId, codes, ct);

        return new Response(codes);
    }

    /// <summary>
    /// Chequeo de permiso individual (helper usado por RequirePermissionAttribute).
    /// </summary>
    public static async Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionCode,
        IPermissionsRepository permissionsRepo,
        IPermissionsCache cache,
        CancellationToken ct = default)
    {
        var codes = await GetUserPermissionsCodesCachedAsync(userId, permissionsRepo, cache, ct);
        return codes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<string>> GetUserPermissionsCodesCachedAsync(
        Guid userId,
        IPermissionsRepository repo,
        IPermissionsCache cache,
        CancellationToken ct)
    {
        var cached = await cache.GetAsync(userId, ct);
        if (cached is not null) return cached;

        var codes = await repo.GetUserPermissionCodesAsync(userId, ct);
        await cache.SetAsync(userId, codes, ct);
        return codes;
    }
}
