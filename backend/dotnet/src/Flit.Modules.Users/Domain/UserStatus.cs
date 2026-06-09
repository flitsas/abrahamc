namespace Flit.Modules.Users.Domain;

/// <summary>
/// Estados validos de un usuario en FLIT 2.0 (ADR-0010).
/// Persisten como VARCHAR(20) en identity.users.status con CHECK constraint.
/// </summary>
public enum UserStatus
{
    ACTIVE,
    INACTIVE,
    BLOCKED,
    DELETED
}
