namespace Flit.Modules.Rbac.Domain;

/// <summary>
/// Aggregate root del modulo RBAC (ADR-0011).
/// Tabla: rbac.roles.
///
/// Roles seed del sistema (es_sistema = true, no eliminables):
///   ADMIN, PROCEDURES_OPERATOR, PROCEDURES_SUPERVISOR, CITIZEN, AUDITOR
/// </summary>
public sealed class Role
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Role() { } // EF Core

    public static Role Create(
        string code,
        string name,
        string? description,
        bool isSystem,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code requerido", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name requerido", nameof(name));

        return new Role
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsSystem = isSystem,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void UpdateProfile(string name, string? description, DateTimeOffset now)
    {
        if (IsSystem)
            throw new InvalidOperationException("Roles del sistema no se pueden editar");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name requerido", nameof(name));
        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (IsSystem && Code == "ADMIN")
            throw new InvalidOperationException("No se puede desactivar el rol ADMIN");
        IsActive = false;
        UpdatedAt = now;
    }
}
