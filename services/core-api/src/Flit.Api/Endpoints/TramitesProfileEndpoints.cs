using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;
using IClock = Flit.SharedKernel.IClock;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;

namespace Flit.Api.Endpoints;

/// <summary>Perfil autoservicio y cambio de contraseña (prereq HU #9420).</summary>
public static class TramitesProfileEndpoints
{
    public sealed record UpdateProfileRequest(string? FullName, string? Phone, string? Locale);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public static void MapTramitesProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity/me")
            .WithTags("Identity - Profile");

        group.MapGet("/profile", async (
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityProfileRepository profiles,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesProfileUseCases.GetProfileAsync(
                tenant.TenantId!.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                profiles,
                ct);

            return result.Match(
                ok => Results.Ok(MapProfile(ok)),
                code => code == TramitesProfileUseCases.ProfileNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : Results.BadRequest(new { error = code }));
        })
        .WithName("TramitesGetMyProfile");

        group.MapPatch("/profile", async (
            UpdateProfileRequest req,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityProfileRepository profiles,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesProfileUseCases.UpdateProfileAsync(
                tenant.TenantId!.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                new TramitesProfileUseCases.UpdateProfileCommand(
                    req.FullName, req.Phone, req.Locale),
                profiles,
                ct);

            return result.Match(
                ok => Results.Ok(MapProfile(ok)),
                code => code == TramitesProfileUseCases.ProfileNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : Results.BadRequest(new { error = code }));
        })
        .WithName("TramitesUpdateMyProfile");

        group.MapPost("/change-password", async (
            ChangePasswordRequest req,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityProfileRepository profiles,
            IIdentityAccountRepository accounts,
            IIdentityOnboardingRepository onboarding,
            IPasswordHasher hasher,
            IClock clock,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesProfileUseCases.ChangePasswordAsync(
                tenant.TenantId!.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                new TramitesProfileUseCases.ChangePasswordCommand(
                    req.CurrentPassword, req.NewPassword),
                profiles,
                accounts,
                onboarding,
                hasher,
                clock,
                ct);

            return result.Match(
                _ => Results.NoContent(),
                code => code switch
                {
                    TramitesPasswordPolicyValidator.PolicyViolationCode =>
                        Results.BadRequest(new
                        {
                            error = code,
                            message = "La contraseña no cumple la política del tenant.",
                        }),
                    TramitesProfileUseCases.InvalidCurrentPasswordCode =>
                        Results.Json(
                            new { error = code, message = "La contraseña actual es incorrecta." },
                            statusCode: StatusCodes.Status403Forbidden),
                    _ => Results.BadRequest(new { error = code }),
                });
        })
        .WithName("TramitesChangeMyPassword");
    }

    private static object MapProfile(Flit.Modules.Identity.Domain.ProfileRow p) => new
    {
        userId = p.UserId,
        tenantId = p.TenantId,
        email = p.Email,
        fullName = p.FullName,
        phone = p.Phone,
        address = p.Address,
        locale = p.Locale,
        timezone = p.Timezone,
        accountState = p.AccountState,
        roleSlugs = p.RoleSlugs,
        rowVersion = p.RowVersion,
    };

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
}
