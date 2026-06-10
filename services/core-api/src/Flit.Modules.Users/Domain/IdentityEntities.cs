namespace Flit.Modules.Users.Domain;

/// <summary>
/// Token de reset de contrasena (ADR-0010 §"Reset password").
/// Tabla identity.password_reset_tokens. TTL 30 min. token_hash es SHA-256
/// del token enviado por email — el plaintext no se persiste.
/// </summary>
public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PasswordResetToken() { } // EF Core

    public static PasswordResetToken Create(
        Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
            throw new ArgumentException("TokenHash debe ser SHA-256 hex (64 chars)", nameof(tokenHash));
        if (expiresAt <= now)
            throw new ArgumentException("ExpiresAt debe ser futuro", nameof(expiresAt));

        return new PasswordResetToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
        };
    }

    public void MarkUsed(DateTimeOffset now)
    {
        if (UsedAt is not null)
            throw new InvalidOperationException("Token ya fue usado");
        if (now > ExpiresAt)
            throw new InvalidOperationException("Token expirado");
        UsedAt = now;
    }
}

/// <summary>
/// Audit log de eventos del usuario (ADR-0010 §"Auditoria").
/// Tabla identity.user_audit_log. Bigserial PK porque es append-only de alta cardinalidad.
/// </summary>
public sealed class UserAuditLog
{
    public long Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Event { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public Guid? ExecutedByUserId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private UserAuditLog() { } // EF Core

    public static UserAuditLog Record(
        Guid userId,
        string @event,
        string? metadataJson,
        Guid? executedByUserId,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(@event))
            throw new ArgumentException("Event requerido", nameof(@event));
        return new UserAuditLog
        {
            UserId = userId,
            Event = @event.Trim().ToUpperInvariant(),
            MetadataJson = metadataJson,
            ExecutedByUserId = executedByUserId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OccurredAt = now,
        };
    }
}

/// <summary>
/// Registro de inconsistencias detectadas entre BD propia y Cognito (ADR-0010 §"Sync").
/// Tabla identity.sync_inconsistencies. Bigserial PK.
/// </summary>
public sealed class SyncInconsistency
{
    public long Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public string? CognitoSub { get; private set; }
    public string? Email { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }

    private SyncInconsistency() { } // EF Core

    public static SyncInconsistency Detect(
        string type, Guid? userId, string? cognitoSub, string? email,
        string? detail, DateTimeOffset now)
    {
        return new SyncInconsistency
        {
            Type = type,
            UserId = userId,
            CognitoSub = cognitoSub,
            Email = email,
            Detail = detail,
            DetectedAt = now,
        };
    }

    public void Resolve(DateTimeOffset now) => ResolvedAt = now;

    public static class Types
    {
        public const string CognitoOrphan = "COGNITO_ORPHAN";
        public const string DbOrphan = "DB_ORPHAN";
        public const string StatusDesynced = "STATUS_DESYNCED";
    }
}
