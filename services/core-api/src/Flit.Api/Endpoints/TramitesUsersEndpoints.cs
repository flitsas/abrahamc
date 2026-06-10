using System.Security.Cryptography;
using System.Text;
using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;
using IClock = Flit.SharedKernel.IClock;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;

namespace Flit.Api.Endpoints;

/// <summary>Invitación de usuarios y gestión de credenciales (HU #9684).</summary>
public static class TramitesUsersEndpoints
{
    public const string ManageUsersPermission = "modulo.identidad.gestionar-usuarios";

    public sealed record InviteUserRequest(
        string Email,
        Guid InvitedRoleId,
        Guid? TenantId);

    public static void MapTramitesUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Identity - Users");

        group.MapPost("/invite", InviteUserHandler)
            .RequireTramitesPermission(ManageUsersPermission)
            .WithName("TramitesInviteUser");
    }

    internal static async Task<IResult> InviteUserHandler(
        InviteUserRequest req,
        HttpContext http,
        ITenantContext tenant,
        ITokenIssuer tokenIssuer,
        IIdentityOnboardingRepository onboarding,
        IConfiguration config,
        IOnboardingEmailNotifier notifier,
        IClock clock,
        CancellationToken ct)
    {
        if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
            return Results.Unauthorized();

        var signingKey = LoadSigningKey(config);
        var baseUrl = config["Identity:Onboarding:ActivationBaseUrl"]
            ?? "http://localhost:5173";

        var result = await TramitesOnboardingUseCases.CreateInvitationAsync(
            new TramitesOnboardingUseCases.CreateInvitationCommand(
                req.Email, req.InvitedRoleId, req.TenantId),
            tenant.TenantId!.Value,
            tenant.IsSuperAdmin,
            tenant.UserId!.Value,
            onboarding,
            signingKey,
            baseUrl,
            notifier,
            clock,
            ct);

        return result.Match(
            ok => Results.Created(
                $"/api/v1/users/invite/{ok.InvitationId}",
                new
                {
                    invitationId = ok.InvitationId,
                    email = ok.Email,
                    expiresAt = ok.ExpiresAt,
                    activationUrl = ok.ActivationUrl,
                    emailQueued = ok.EmailQueued,
                }),
            MapInviteError);
    }

    private static byte[] LoadSigningKey(IConfiguration config)
    {
        var key = config["Identity:Onboarding:SigningKey"]
            ?? "flit-dev-onboarding-signing-key-change-in-production";
        return SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    private static bool TryEnsureAuthenticated(
        ITenantContext tenant,
        ITokenIssuer tokenIssuer,
        HttpContext http)
    {
        if (tenant.UserId.HasValue && tenant.TenantId.HasValue)
            return true;

        var access = AuthCookieWriter.ReadAccessFromRequest(http.Request);
        if (string.IsNullOrWhiteSpace(access))
            return false;

        var subject = tokenIssuer.ValidateTramitesAccess(access);
        if (subject is null)
            return false;

        tenant.Apply(
            subject.TenantId, subject.UserId, subject.IsSuperAdmin,
            trafficAgencyId: null,
            http.Items[Middleware.CorrelationIdMiddleware.ItemKey] as Guid?,
            http.Connection.RemoteIpAddress?.ToString());

        return true;
    }

    private static IResult MapInviteError(string code) => code switch
    {
        TramitesOnboardingUseCases.EmailExistsCode => Results.Conflict(new
        {
            error = code,
            message = "Ya existe una cuenta activa con ese correo.",
        }),
        TramitesOnboardingUseCases.RoleNotFoundCode => Results.BadRequest(new
        {
            error = code,
            message = "Rol invitado no encontrado.",
        }),
        TramitesOnboardingUseCases.ForbiddenTenantCode => Results.Json(
            new { error = code, message = "No puede invitar usuarios en otro tenant." },
            statusCode: StatusCodes.Status403Forbidden),
        "INVALID_EMAIL" => Results.BadRequest(new { error = code, message = "Correo inválido." }),
        _ => Results.BadRequest(new { error = code }),
    };
}
