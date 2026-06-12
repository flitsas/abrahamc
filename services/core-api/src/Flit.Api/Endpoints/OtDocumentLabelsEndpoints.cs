using Flit.Api.Auth;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Identity.Ports;

namespace Flit.Api.Endpoints;

/// <summary>HU #9700 — CRUD etiquetas documentales OT.</summary>
public static class OtDocumentLabelsEndpoints
{
    public sealed record CreateDocumentLabelRequest(
        string LabelKey,
        string DisplayName,
        bool ExcludeFromBundle = false);

    public static void MapOtDocumentLabelsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ot/agencies/{agencyId:guid}/document-labels")
            .WithTags("OT — Etiquetas documentales");

        group.MapGet("/", async (
            Guid agencyId,
            IOtDocumentLabelRepository repo,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanRead(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var items = await repo.ListByAgencyAsync(agencyId, ct);
            return Results.Ok(items.Select(i => new
            {
                id = i.Id,
                label_key = i.LabelKey,
                display_name = i.DisplayName,
                exclude_from_bundle = i.ExcludeFromBundle,
            }));
        })
        .WithName("ListOtDocumentLabels")
        .WithSummary("Lista etiquetas documentales del OT");

        group.MapPost("/", async (
            Guid agencyId,
            CreateDocumentLabelRequest req,
            IOtDocumentLabelRepository repo,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanMutate(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(req.LabelKey) || string.IsNullOrWhiteSpace(req.DisplayName))
                return Results.BadRequest(new { error = "label_key y display_name son requeridos." });

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var created = await repo.CreateAsync(
                agencyId,
                req.LabelKey.Trim(),
                req.DisplayName.Trim(),
                req.ExcludeFromBundle,
                actorId,
                ct);

            return Results.Created(
                $"/api/v1/ot/agencies/{agencyId}/document-labels/{created.Id}",
                new
                {
                    id = created.Id,
                    label_key = created.LabelKey,
                    display_name = created.DisplayName,
                    exclude_from_bundle = created.ExcludeFromBundle,
                });
        })
        .WithName("CreateOtDocumentLabel")
        .WithSummary("Crea etiqueta documental personalizada");

        group.MapDelete("/{labelId:guid}", async (
            Guid agencyId,
            Guid labelId,
            IOtDocumentLabelRepository repo,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanMutate(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var deleted = await repo.SoftDeleteAsync(agencyId, labelId, actorId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteOtDocumentLabel")
        .WithSummary("Elimina etiqueta documental (soft-delete)");
    }
}
