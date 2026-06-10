using Flit.Modules.Auth.Application;
using Flit.Modules.Auth.Domain;
using Flit.Modules.Auth.Ports;
using Flit.Modules.Users.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// REST endpoints para Auth (Fase 7 — Hybrid Cognito + MFA TOTP).
/// Contrato canonico en contracts/openapi/core-api.v1.yaml (sync pendiente).
///
/// Stateless: el JWT middleware (JwtBearer + JWKS Cognito) extrae el userId del
/// access_token validado. Los endpoints "/me" usan ese sub para buscar el User
/// en BD local (sin self-claim).
///
/// IMPORTANTE: para Fase 7 baseline, EnableMfa/DisableMfa requieren userId via
/// query param. Cuando el middleware JwtBearer + claim mapping (Fase 7.1) este
/// activo, se extraen del HttpContext.User.
/// </summary>
public static class AuthEndpoints
{
    // ─── DTOs ───────────────────────────────────────────────────────
    public sealed record LoginRequest(string Email, string Password);
    public sealed record ValidateMfaRequest(Guid SessionId, string Code);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record RequestPasswordResetRequest(string Email);
    public sealed record ConfirmPasswordResetRequest(string ResetToken, string NewPassword);
    public sealed record EnableMfaRequest(Guid UserId);
    public sealed record DisableMfaRequest(Guid UserId);

    // TTL de sesion MFA (5 min) — RFC 6238 ventana + tolerancia humana.
    private static readonly TimeSpan MfaSessionTtl = TimeSpan.FromMinutes(5);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        // ─── POST /login ──────────────────────────────────────────────
        group.MapPost("/login", async (
            LoginRequest req,
            HttpContext http,
            IUsersRepository users,
            ICognitoDirectory cognito,
            IMfaSessionStore mfaStore,
            IClock clock,
            CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            var cmd = new Login.Command(req.Email, req.Password, ip, ua);

            var result = await Login.HandleAsync(
                cmd, users, cognito, mfaStore, clock, MfaSessionTtl, ct);

            return result.Match(
                ok => ok switch
                {
                    Login.Response.TokensIssued ti =>
                        Results.Ok(new { mfaRequired = false, tokens = ti.Tokens, user = ti.User }),
                    Login.Response.MfaRequired mr =>
                        Results.Ok(new { mfaRequired = true, sessionId = mr.SessionId }),
                    _ => Results.Problem("Unknown login response"),
                },
                err => MapLoginError(err));
        })
        .WithName("Login");

        // ─── POST /mfa — validar codigo TOTP ──────────────────────────
        group.MapPost("/mfa", async (
            ValidateMfaRequest req,
            IMfaSessionStore mfaStore,
            IUsersRepository users,
            ICognitoDirectory cognito,
            Aes256GcmSecretCipher cipher,
            TotpService totp,
            IClock clock,
            CancellationToken ct) =>
        {
            var cmd = new ValidateMfa.Command(req.SessionId, req.Code);
            var result = await ValidateMfa.HandleAsync(
                cmd, mfaStore, users, cognito, cipher, totp, clock, ct);
            return result.Match(
                ok => Results.Ok(new { tokens = ok.Tokens, user = ok.User }),
                err => MapValidateMfaError(err));
        })
        .WithName("ValidateMfa");

        // ─── POST /refresh ────────────────────────────────────────────
        group.MapPost("/refresh", async (
            RefreshRequest req,
            IUsersRepository users,
            ICognitoDirectory cognito,
            CancellationToken ct) =>
        {
            var result = await RefreshAuthTokens.HandleAsync(
                new RefreshAuthTokens.Command(req.RefreshToken), users, cognito, ct);
            return result.Match(
                ok => Results.Ok(new { tokens = ok.Tokens }),
                err => MapRefreshError(err));
        })
        .WithName("RefreshTokens");

        // ─── POST /logout — best-effort, siempre 204 ──────────────────
        group.MapPost("/logout", async (
            LogoutRequest req, ICognitoDirectory cognito, CancellationToken ct) =>
        {
            await Logout.HandleAsync(new Logout.Command(req.RefreshToken), cognito, ct);
            return Results.NoContent();
        })
        .WithName("Logout");

        // ─── POST /password-reset/request — anti-enumeration: siempre 202 ──
        group.MapPost("/password-reset/request", async (
            RequestPasswordResetRequest req,
            HttpContext http,
            IUsersRepository users,
            IPasswordResetTokensRepository tokens,
            IClock clock,
            CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            await RequestPasswordReset.HandleAsync(
                new RequestPasswordReset.Command(req.Email, ip, ua),
                users, tokens, clock,
                onTokenGenerated: (email, rawToken) =>
                {
                    // TODO Fase 7.2: publicar al bus RabbitMQ (flit.notifications):
                    //   { type=PASSWORD_RESET, channel=EMAIL, to=email, payload={resetToken} }
                    // Consumer in-process en Flit.Modules.Notifications (post ADR-0014)
                    // consume y manda el correo.
                    // Serilog directo (evita CA1848/CA1873 de ILogger generico).
                    Serilog.Log.Information(
                        "Password reset token generated for {EmailMasked} (dev-only log)",
                        MaskEmail(email));
                },
                ct);
            return Results.Accepted();
        })
        .WithName("RequestPasswordReset");

        // ─── POST /password-reset/confirm ─────────────────────────────
        group.MapPost("/password-reset/confirm", async (
            ConfirmPasswordResetRequest req,
            IPasswordResetTokensRepository tokens,
            IUsersRepository users,
            ICognitoDirectory cognito,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await ConfirmPasswordReset.HandleAsync(
                new ConfirmPasswordReset.Command(req.ResetToken, req.NewPassword),
                tokens, users, cognito, clock, ct);
            return result.Match(
                _ => Results.NoContent(),
                err => MapConfirmResetError(err));
        })
        .WithName("ConfirmPasswordReset");

        // ─── POST /mfa/enable — devuelve secret + URI para QR ─────────
        group.MapPost("/mfa/enable", async (
            EnableMfaRequest req,
            IUsersRepository users,
            Aes256GcmSecretCipher cipher,
            TotpService totp,
            IConfiguration config,
            IClock clock,
            CancellationToken ct) =>
        {
            var issuer = config["Mfa:TotpIssuer"] ?? "FLIT";
            var result = await EnableMfa.HandleAsync(
                new EnableMfa.Command(req.UserId), users, cipher, totp, issuer, clock, ct);
            return result.Match(
                ok => Results.Ok(new { secret = ok.SecretBase32, otpAuthUri = ok.OtpAuthUri }),
                err => MapEnableMfaError(err));
        })
        .WithName("EnableMfa");

        // ─── POST /mfa/disable ────────────────────────────────────────
        group.MapPost("/mfa/disable", async (
            DisableMfaRequest req,
            IUsersRepository users,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await DisableMfa.HandleAsync(
                new DisableMfa.Command(req.UserId), users, clock, ct);
            return result.Match(
                _ => Results.NoContent(),
                err => Results.NotFound(new { error = err }));
        })
        .WithName("DisableMfa");
    }

    // ─── Error mappers ───────────────────────────────────────────────
    private static IResult MapLoginError(Login.LoginError err) => err switch
    {
        Login.LoginError.InvalidCredentials =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 401),
        Login.LoginError.UserNotActive =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 403),
        Login.LoginError.CognitoOrphan =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 409),
        Login.LoginError.CognitoUnavailable =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 503),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapValidateMfaError(ValidateMfa.ValidateMfaError err) => err switch
    {
        ValidateMfa.ValidateMfaError.SessionExpired =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 410),
        ValidateMfa.ValidateMfaError.TooManyAttempts =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 429),
        ValidateMfa.ValidateMfaError.InvalidCode =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 401),
        ValidateMfa.ValidateMfaError.UserNotFound or
        ValidateMfa.ValidateMfaError.MfaNotEnabled =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 404),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapRefreshError(RefreshAuthTokens.RefreshError err) => err switch
    {
        RefreshAuthTokens.RefreshError.InvalidRefreshToken =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 401),
        RefreshAuthTokens.RefreshError.UserNotActive =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 403),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapConfirmResetError(ConfirmPasswordReset.ConfirmError err) => err switch
    {
        ConfirmPasswordReset.ConfirmError.InvalidToken =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 400),
        ConfirmPasswordReset.ConfirmError.CognitoFailed =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 502),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapEnableMfaError(EnableMfa.EnableMfaError err) => err switch
    {
        EnableMfa.EnableMfaError.UserNotFound =>
            Results.NotFound(new { error = err.Code, message = err.Message }),
        EnableMfa.EnableMfaError.UserNotActive =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 403),
        EnableMfa.EnableMfaError.AlreadyEnabled =>
            Results.Conflict(new { error = err.Code, message = err.Message }),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    /// <summary>Enmascara user@domain.tld → u**r@d***.tld (Habeas Data).</summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at < 2) return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        var maskedLocal = local.Length <= 2
            ? new string('*', local.Length)
            : $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}";
        return $"{maskedLocal}@{domain}";
    }
}
