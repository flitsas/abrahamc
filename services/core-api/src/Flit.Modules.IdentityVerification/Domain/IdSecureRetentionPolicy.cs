namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Política de retención de evidencias biométricas IDSecure (Ley 1581 — HU #9490).
/// </summary>
public static class IdSecureRetentionPolicy
{
    /// <summary>Retención por defecto: 90 días post-captura (ajustable vía config).</summary>
    public const int DefaultRetentionDays = 90;

    public static DateTimeOffset ComputeCutoff(DateTimeOffset now, int retentionDays) =>
        now.AddDays(-retentionDays);

    public static bool IsExpired(DateTimeOffset capturedAt, DateTimeOffset now, int retentionDays) =>
        capturedAt < ComputeCutoff(now, retentionDays);
}
