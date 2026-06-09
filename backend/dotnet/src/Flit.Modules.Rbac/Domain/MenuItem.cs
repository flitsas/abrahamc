namespace Flit.Modules.Rbac.Domain;

/// <summary>
/// Aggregate root del modulo RBAC (ADR-0011).
/// Tabla: rbac.menu_items.
///
/// Estructura de arbol con parent_id self-FK. La raiz son items con
/// ParentId = null. El endpoint GET /api/v1/menu/me devuelve el arbol
/// filtrado por roles del usuario autenticado.
/// </summary>
public sealed class MenuItem
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public string? FrontendPath { get; private set; }
    public short SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsSeparator { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private MenuItem() { } // EF Core

    public static MenuItem Create(
        string code,
        Guid? parentId,
        string label,
        string? icon,
        string? frontendPath,
        short sortOrder,
        bool isSeparator,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code requerido", nameof(code));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label requerido", nameof(label));

        return new MenuItem
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            ParentId = parentId,
            Label = label.Trim(),
            Icon = icon?.Trim(),
            FrontendPath = frontendPath?.Trim(),
            SortOrder = sortOrder,
            IsActive = true,
            IsVisible = true,
            IsSeparator = isSeparator,
            CreatedAt = now,
        };
    }

    public void Update(
        string label,
        string? icon,
        string? frontendPath,
        short sortOrder,
        DateTimeOffset _)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label requerido", nameof(label));
        Label = label.Trim();
        Icon = icon?.Trim();
        FrontendPath = frontendPath?.Trim();
        SortOrder = sortOrder;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    public void Show() => IsVisible = true;
    public void Hide() => IsVisible = false;
}
