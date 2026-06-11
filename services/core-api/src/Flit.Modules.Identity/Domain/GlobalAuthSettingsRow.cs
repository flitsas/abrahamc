namespace Flit.Modules.Identity.Domain;

/// <summary>
/// Configuración global singleton de autenticación (identity.global_auth_settings).
/// Un solo registro para toda la plataforma; no es por usuario ni por tenant.
/// </summary>
public sealed record GlobalAuthSettingsRow(
    int InvitationTtlMinutes,
    int PasswordResetTtlMinutes,
    int AccessTokenTtlMinutes,
    int RefreshTokenTtlDays)
{
    public static GlobalAuthSettingsRow Default { get; } = new(60, 30, 15, 7);

    public TimeSpan InvitationTtl => TimeSpan.FromMinutes(InvitationTtlMinutes);

    public TimeSpan PasswordResetTtl => TimeSpan.FromMinutes(PasswordResetTtlMinutes);

    public TimeSpan AccessTokenTtl => TimeSpan.FromMinutes(AccessTokenTtlMinutes);

    public TimeSpan RefreshTokenTtl => TimeSpan.FromDays(RefreshTokenTtlDays);
}
