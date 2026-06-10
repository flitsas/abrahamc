using Flit.Api.Auth;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// HU #9461 (Feature #9379) — API gestión orden consolidado por OT.
/// GET  /api/v1/ot/agencies/{agencyId}/consolidated-order
/// PUT  /api/v1/ot/agencies/{agencyId}/consolidated-order
/// DELETE /api/v1/ot/agencies/{agencyId}/consolidated-order/items/{itemId}
/// </summary>
public static class OtConsolidatedOrderEndpoints
{
    private static readonly Guid DefaultActorUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public sealed record ConsolidatedOrderItemResponse(
        Guid Id,
        int Position,
        string Source,
        Guid? ProcedureDocumentCatalogId,
        string? CatalogCode,
        string? CatalogName,
        string? CustomLabel);

    public sealed record ConsolidatedOrderResponse(
        Guid OrderId,
        int Version,
        IReadOnlyList<ConsolidatedOrderItemResponse> Items);

    public sealed record SaveConsolidatedOrderItemRequest(
        Guid? Id,
        int Position,
        string Source,
        Guid? ProcedureDocumentCatalogId,
        string? CustomLabel);

    public sealed record SaveConsolidatedOrderRequest(
        IReadOnlyList<SaveConsolidatedOrderItemRequest> Items);

    public static void MapOtConsolidatedOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ot/agencies/{agencyId:guid}/consolidated-order")
            .WithTags("OT — Orden consolidado");

        group.MapGet("/", async (
            Guid agencyId,
            IOtConsolidatedOrderRepository repo,
            FlitDbContext db,
            IClock clock,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanRead(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var actorId = session.ActorUserId ?? DefaultActorUserId;
            var cmd = new GetOtConsolidatedOrder.Command(agencyId, actorId);
            var result = await GetOtConsolidatedOrder.HandleAsync(
                cmd, repo, async c => await db.SaveChangesAsync(c), clock, ct);

            return result.IsSuccess
                ? Results.Ok(MapResponse(result.Value))
                : Results.UnprocessableEntity(new { error = result.Error });
        })
        .WithName("GetOtConsolidatedOrder")
        .WithSummary("Obtiene el orden activo del consolidado; lazy-create si no existe");

        group.MapPut("/", async (
            Guid agencyId,
            SaveConsolidatedOrderRequest req,
            IOtConsolidatedOrderRepository repo,
            FlitDbContext db,
            IClock clock,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanMutate(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var actorId = session.ActorUserId ?? DefaultActorUserId;
            var items = (req.Items ?? [])
                .Select(i => new SaveConsolidatedOrderItemCommand(
                    i.Id, i.Position, i.Source, i.ProcedureDocumentCatalogId, i.CustomLabel))
                .ToList();

            var cmd = new SaveOtConsolidatedOrder.Command(agencyId, actorId, items);
            var result = await SaveOtConsolidatedOrder.HandleAsync(
                cmd, repo, async c => await db.SaveChangesAsync(c), clock, ct);

            if (!result.IsSuccess)
                return Results.UnprocessableEntity(new { error = result.Error });

            var readCmd = new GetOtConsolidatedOrder.Command(agencyId, actorId);
            var read = await GetOtConsolidatedOrder.HandleAsync(
                readCmd, repo, async c => await db.SaveChangesAsync(c), clock, ct);

            return read.IsSuccess
                ? Results.Ok(MapResponse(read.Value))
                : Results.Ok(new { saved = true });
        })
        .WithName("SaveOtConsolidatedOrder")
        .WithSummary("Reemplaza ítems y positions del orden activo en transacción (AC1)");

        group.MapDelete("/items/{itemId:guid}", async (
            Guid agencyId,
            Guid itemId,
            IOtConsolidatedOrderRepository repo,
            FlitDbContext db,
            IClock clock,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanMutate(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var actorId = session.ActorUserId ?? DefaultActorUserId;
            var cmd = new RemoveOtConsolidatedOrderItem.Command(agencyId, itemId, actorId);
            var result = await RemoveOtConsolidatedOrderItem.HandleAsync(
                cmd, repo, async c => await db.SaveChangesAsync(c), clock, ct);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Error.Message });
        })
        .WithName("RemoveOtConsolidatedOrderItem")
        .WithSummary("Quita un ítem del orden (AC2 — no borra archivos MinIO)");
    }

    private static ConsolidatedOrderResponse MapResponse(GetOtConsolidatedOrder.Result result) =>
        new(
            result.OrderId,
            result.Version,
            result.Items.Select(i => new ConsolidatedOrderItemResponse(
                i.Id,
                i.Position,
                i.Source,
                i.ProcedureDocumentCatalogId,
                i.CatalogCode,
                i.CatalogName,
                i.CustomLabel)).ToList());
}

/// <summary>Permisos OT compartidos entre endpoints de consola (#9457 / #9461).</summary>
internal static class OtEndpointAuth
{
    private const string OtOperatorPermission = "modulo.ot.operador";
    private const string OtSuperAdminPermission = "modulo.ot.superadmin";

    public static bool CanRead(HttpContext ctx, ITokenIssuer tokenIssuer, ICompaniesSessionContext session) =>
        HasPermission(ctx, tokenIssuer, session, OtOperatorPermission)
        || HasPermission(ctx, tokenIssuer, session, OtSuperAdminPermission);

    public static bool CanMutate(HttpContext ctx, ITokenIssuer tokenIssuer, ICompaniesSessionContext session) =>
        HasPermission(ctx, tokenIssuer, session, OtSuperAdminPermission);

    private static bool HasPermission(
        HttpContext ctx,
        ITokenIssuer tokenIssuer,
        ICompaniesSessionContext session,
        string slug)
    {
        if (session.IsSuperAdmin)
            return true;

        var cookie = ctx.Request.Cookies[AuthCookieNames.Access];
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            var subject = tokenIssuer.ValidateTramitesAccess(cookie);
            if (subject?.PermissionSlugs.Contains(slug, StringComparer.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    }
}
