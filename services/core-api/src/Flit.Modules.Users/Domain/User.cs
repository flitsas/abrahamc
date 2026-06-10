namespace Flit.Modules.Users.Domain;

/// <summary>
/// Aggregate root del modulo Users (ADR-0010).
/// Tabla: identity.users.
///
/// Reglas:
///   - cognito_sub se asocia tras AdminCreateUser en Cognito (puede ser null durante
///     la creacion antes de obtener respuesta de Cognito).
///   - status solo cambia via metodos del dominio que validan transiciones.
///   - mfa_secret es opcional; cuando MFA esta deshabilitado es null.
///   - soft delete: deleted_at se setea; los registros se filtran con HasQueryFilter.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string? CognitoSub { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? DocumentType { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? Phone { get; private set; }
    public UserStatus Status { get; private set; }
    public bool MfaEnabled { get; private set; }
    public MfaSecret? MfaSecret { get; private set; }
    public DateTimeOffset? MfaEnabledAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private User() { } // EF Core

    /// <summary>
    /// Factory para creacion. cognito_sub se asocia despues con LinkCognitoSub.
    /// </summary>
    public static User Create(
        string email,
        string fullName,
        string? documentType,
        string? documentNumber,
        string? phone,
        Guid? createdByUserId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email requerido", nameof(email));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("FullName requerido", nameof(fullName));

        return new User
        {
            Id = Guid.CreateVersion7(),
            CognitoSub = null,
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            Phone = phone,
            Status = UserStatus.ACTIVE,
            MfaEnabled = false,
            MfaSecret = null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = createdByUserId,
        };
    }

    /// <summary>Tras AdminCreateUser en Cognito.</summary>
    public void LinkCognitoSub(string cognitoSub, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(cognitoSub))
            throw new ArgumentException("CognitoSub requerido", nameof(cognitoSub));
        if (CognitoSub is not null)
            throw new InvalidOperationException("Usuario ya tiene cognito_sub asociado");
        CognitoSub = cognitoSub;
        UpdatedAt = now;
    }

    /// <summary>Status del directorio: transiciones validas.</summary>
    public void ChangeStatus(UserStatus newStatus, DateTimeOffset now)
    {
        if (Status == UserStatus.DELETED)
            throw new InvalidOperationException("Usuario eliminado no puede cambiar status");
        if (newStatus == Status)
            return;
        Status = newStatus;
        UpdatedAt = now;
    }

    /// <summary>Soft delete (DELETED + deleted_at).</summary>
    public void Delete(DateTimeOffset now)
    {
        Status = UserStatus.DELETED;
        DeletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Habilitar MFA: requiere secret cifrado externamente.</summary>
    public void EnableMfa(MfaSecret secret, DateTimeOffset now)
    {
        if (Status != UserStatus.ACTIVE)
            throw new InvalidOperationException("Solo usuarios ACTIVE pueden habilitar MFA");
        MfaEnabled = true;
        MfaSecret = secret ?? throw new ArgumentNullException(nameof(secret));
        MfaEnabledAt = now;
        UpdatedAt = now;
    }

    public void DisableMfa(DateTimeOffset now)
    {
        MfaEnabled = false;
        MfaSecret = null;
        MfaEnabledAt = null;
        UpdatedAt = now;
    }

    public void RegisterLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        UpdatedAt = now;
    }

    /// <summary>Update de datos basicos del perfil.</summary>
    public void UpdateProfile(
        string fullName,
        string? documentType,
        string? documentNumber,
        string? phone,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("FullName requerido", nameof(fullName));
        FullName = fullName.Trim();
        DocumentType = documentType;
        DocumentNumber = documentNumber;
        Phone = phone;
        UpdatedAt = now;
    }
}
