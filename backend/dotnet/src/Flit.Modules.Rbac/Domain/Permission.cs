namespace Flit.Modules.Rbac.Domain;

/// <summary>
/// Aggregate root del modulo RBAC (ADR-0011).
/// Tabla: rbac.permissions.
///
/// Convencion de codes: MODULE.ACTION_SPECIFIC.
///   PROCEDURES.LIST, PROCEDURES.CREATE_REGISTRATION, USERS.CHANGE_STATUS, etc.
/// </summary>
public sealed class Permission
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Module { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Permission() { } // EF Core

    public static Permission Create(
        string code,
        string name,
        string? description,
        string module,
        bool isSystem,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code requerido", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name requerido", nameof(name));
        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException("Module requerido", nameof(module));

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (!normalizedCode.Contains('.'))
            throw new ArgumentException(
                "Code debe seguir formato MODULE.ACTION (ej PROCEDURES.LIST)",
                nameof(code));

        return new Permission
        {
            Id = Guid.CreateVersion7(),
            Code = normalizedCode,
            Name = name.Trim(),
            Description = description?.Trim(),
            Module = module.Trim().ToUpperInvariant(),
            IsSystem = isSystem,
            CreatedAt = now,
        };
    }
}
