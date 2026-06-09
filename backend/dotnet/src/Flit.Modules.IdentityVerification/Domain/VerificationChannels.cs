namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>Canales soportados (AC1 TRA-03): SMS, correo, OTP y liveness.</summary>
public static class VerificationChannels
{
    public const string Sms = "sms";
    public const string Email = "email";
    public const string Otp = "otp";
    public const string Liveness = "liveness";

    public static readonly IReadOnlyList<string> All =
        [Sms, Email, Otp, Liveness];
}
