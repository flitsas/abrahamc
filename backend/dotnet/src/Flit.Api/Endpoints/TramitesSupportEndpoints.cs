using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;

namespace Flit.Api.Endpoints;

/// <summary>Tickets de soporte (prereq HU #9420/#9421).</summary>
public static class TramitesSupportEndpoints
{
    public sealed record CreateTicketRequest(string Subject, string Body, string? Category);

    public sealed record UpdateTicketRequest(string? Status, Guid? AssignedToUserId);

    public static void MapTramitesSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity/support/tickets")
            .WithTags("Identity - Support");

        group.MapGet("/", async (
            Guid? tenantId,
            Guid? reporterUserId,
            string? status,
            int? page,
            int? limit,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            ITramitesPermissionVerifier verifier,
            HttpContext http,
            IIdentitySupportRepository support,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var effectiveTenant = ResolveTenantId(tenant, tenantId);
            if (effectiveTenant is null)
                return TenantRequired();

            var adminView = await verifier.HasSlugAsync(
                tenant.UserId!.Value,
                tenant.TenantId!.Value,
                tenant.IsSuperAdmin,
                "modulo.companias.crud-total",
                ct);
            var result = await TramitesSupportUseCases.ListTicketsAsync(
                effectiveTenant.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                adminView,
                tenantId.HasValue,
                reporterUserId,
                status,
                page ?? 1,
                limit ?? 20,
                support,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    items = ok.Items.Select(MapTicket),
                    total = ok.Total,
                    page = page ?? 1,
                    limit = limit ?? 20,
                }),
                code => Results.BadRequest(new { error = code }));
        })
        .WithName("TramitesListSupportTickets");

        group.MapPost("/", async (
            CreateTicketRequest req,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentitySupportRepository support,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var effectiveTenant = ResolveTenantId(tenant, tenantId);
            if (effectiveTenant is null)
                return TenantRequired();

            var result = await TramitesSupportUseCases.CreateTicketAsync(
                effectiveTenant.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                new TramitesSupportUseCases.CreateTicketCommand(
                    req.Subject, req.Body, req.Category),
                support,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/identity/support/tickets/{ok.Id}", MapTicket(ok)),
                code => Results.BadRequest(new { error = code, message = "Datos inválidos." }));
        })
        .WithName("TramitesCreateSupportTicket");

        group.MapGet("/{id:guid}", async (
            Guid id,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            ITramitesPermissionVerifier verifier,
            HttpContext http,
            IIdentitySupportRepository support,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var effectiveTenant = ResolveTenantId(tenant, tenantId);
            if (effectiveTenant is null)
                return TenantRequired();

            var adminView = await verifier.HasSlugAsync(
                tenant.UserId!.Value,
                tenant.TenantId!.Value,
                tenant.IsSuperAdmin,
                "modulo.companias.crud-total",
                ct);
            var result = await TramitesSupportUseCases.GetTicketAsync(
                effectiveTenant.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                adminView,
                id,
                support,
                ct);

            return result.Match(
                ok => Results.Ok(MapTicket(ok)),
                code => Results.NotFound(new { error = code }));
        })
        .WithName("TramitesGetSupportTicket");

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateTicketRequest req,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentitySupportRepository support,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var effectiveTenant = ResolveTenantId(tenant, tenantId);
            if (effectiveTenant is null)
                return TenantRequired();

            var result = await TramitesSupportUseCases.UpdateTicketAsync(
                effectiveTenant.Value,
                tenant.UserId!.Value,
                tenant.IsSuperAdmin,
                id,
                new TramitesSupportUseCases.UpdateTicketCommand(req.Status, req.AssignedToUserId),
                support,
                ct);

            return result.Match(
                ok => Results.Ok(MapTicket(ok)),
                code => code == TramitesSupportUseCases.TicketNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : Results.BadRequest(new { error = code }));
        })
        .RequireTramitesPermission("modulo.companias.crud-total")
        .WithName("TramitesUpdateSupportTicket");
    }

    private static Guid? ResolveTenantId(ITenantContext tenant, Guid? requestTenantId)
    {
        if (tenant.IsSuperAdmin && requestTenantId.HasValue)
            return requestTenantId;

        return tenant.TenantId;
    }

    private static IResult TenantRequired() =>
        Results.BadRequest(new
        {
            error = TramitesAdminContext.TenantContextRequiredCode,
            message = "Debe indicar tenantId en la consulta (selector de compañía).",
        });

    private static object MapTicket(Flit.Modules.Identity.Domain.SupportTicketRow t) => new
    {
        id = t.Id,
        tenantId = t.TenantId,
        tenantName = t.TenantName,
        reporterUserId = t.ReporterUserId,
        reporterEmail = t.ReporterEmail,
        reporterFullName = t.ReporterFullName,
        assignedToUserId = t.AssignedToUserId,
        subject = t.Subject,
        body = t.Body,
        category = t.Category,
        status = t.Status,
        createdAt = t.CreatedAt,
        rowVersion = t.RowVersion,
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
