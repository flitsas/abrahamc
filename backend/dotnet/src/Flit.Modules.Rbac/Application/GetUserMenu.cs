using Flit.Modules.Rbac.Domain;
using Flit.Modules.Rbac.Ports;

namespace Flit.Modules.Rbac.Application;

/// <summary>
/// Devuelve el arbol de menu filtrado por roles del usuario (ADR-0011).
/// Endpoint: GET /api/v1/menu/me.
/// </summary>
public static class GetUserMenu
{
    public sealed record Query(Guid UserId);

    public sealed record MenuNode(
        Guid Id,
        string Code,
        string Label,
        string? Icon,
        string? Path,
        bool IsSeparator,
        short SortOrder,
        IReadOnlyList<MenuNode> Children);

    public sealed record Response(IReadOnlyList<MenuNode> Menu);

    public static async Task<Response> HandleAsync(
        Query query,
        IMenuItemsRepository menuRepo,
        CancellationToken ct = default)
    {
        var visibleItems = await menuRepo.GetVisibleForUserAsync(query.UserId, ct);

        // Construye arbol: agrupa por ParentId
        var byParent = visibleItems
            .GroupBy(m => m.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());

        IReadOnlyList<MenuNode> BuildChildren(Guid parentId)
        {
            if (!byParent.TryGetValue(parentId, out var children))
                return [];
            return [.. children
                .Where(c => c.IsActive && c.IsVisible)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Label)
                .Select(c => new MenuNode(
                    Id: c.Id,
                    Code: c.Code,
                    Label: c.Label,
                    Icon: c.Icon,
                    Path: c.FrontendPath,
                    IsSeparator: c.IsSeparator,
                    SortOrder: c.SortOrder,
                    Children: BuildChildren(c.Id)))];
        }

        var rootNodes = BuildChildren(Guid.Empty);
        return new Response(rootNodes);
    }
}
