namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Entrada append-only en audit.data_access_log (HU #9490 AC2).
/// </summary>
public sealed class DataAccessLogEntry
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string SchemaName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public Guid? RecordId { get; init; }
    public string AccessType { get; init; } = DataAccessTypes.Read;
    public string? Purpose { get; init; }
    public DateTimeOffset AccessedAt { get; init; }
    public Guid? RequestId { get; init; }
    public string? IpAddress { get; init; }
}

public static class DataAccessTypes
{
    public const string Read = "read";
    public const string Download = "download";
    public const string Presign = "presign";
    public const string Export = "export";
}

public static class IdSecureHabeasDataTables
{
    public const string Schema = "identity_verification";
    public const string VerificationSessions = "verification_sessions";
}
