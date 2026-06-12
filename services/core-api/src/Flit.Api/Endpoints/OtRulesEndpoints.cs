using System.Text.Json;
using Flit.Api.Auth;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Application;
using Flit.Modules.Identity.Ports;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// HU #9456 OT-03 — Constructor de reglas de OT.
/// POST   /api/v1/ot/agencies/{agencyId}/rules           → crear regla (AC1 positivo / AC2 negativo)
/// PATCH  /api/v1/ot/agencies/{agencyId}/rules/{id}/toggle → hot-swap is_active (AC1)
/// GET    /api/v1/ot/agencies/{agencyId}/rules            → listar reglas activas
/// </summary>
public static class OtRulesEndpoints
{
    private static readonly Guid DefaultActorUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public sealed record OtRuleListItem(
        Guid Id,
        string Name,
        string TriggerEvent,
        JsonElement ConditionTree,
        JsonElement Actions,
        int Priority,
        bool IsActive);

    public sealed record CreateRuleRequest(
        string Name,
        string TriggerEvent,
        string ConditionTreeJson,
        string ActionsJson,
        int Priority = 100,
        DateTimeOffset? ValidFrom = null,
        DateTimeOffset? ValidUntil = null,
        Guid? ActorUserId = null);

    public static void MapOtRulesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ot/agencies/{agencyId:guid}/rules")
            .WithTags("OT — Constructor de Reglas");

        // POST — crear regla (AC1 + AC2)
        group.MapPost("/", async (
            Guid agencyId,
            CreateRuleRequest req,
            IOtRuleRepository repo,
            FlitDbContext db,
            IClock clock,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanManageRules(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name es requerido." });

            var actorId = req.ActorUserId ?? session.ActorUserId ?? DefaultActorUserId;

            var cmd = new CreateOtRule.Command(
                TrafficAgencyId: agencyId,
                Name: req.Name,
                TriggerEvent: req.TriggerEvent ?? "on_submit",
                ConditionTreeJson: req.ConditionTreeJson ?? "{}",
                ActionsJson: req.ActionsJson ?? "[]",
                Priority: req.Priority,
                ValidFrom: req.ValidFrom,
                ValidUntil: req.ValidUntil,
                ActorUserId: actorId);

            var result = await CreateOtRule.HandleAsync(
                cmd, repo, async c => await db.SaveChangesAsync(c), clock, ct);

            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/ot/agencies/{agencyId}/rules/{result.Value.RuleId}",
                    new { rule_id = result.Value.RuleId, name = result.Value.Name, is_active = result.Value.IsActive })
                : Results.UnprocessableEntity(new { error = result.Error });
        })
        .WithName("CreateOtRule")
        .WithSummary("Crea una regla OT con validación de condition_tree y actions (AC1+AC2)");

        // PATCH — toggle hot-swap is_active (AC1)
        group.MapPatch("/{ruleId:guid}/toggle", async (
            Guid agencyId,
            Guid ruleId,
            IOtRuleRepository repo,
            FlitDbContext db,
            IClock clock,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanManageRules(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var actorId = session.ActorUserId ?? DefaultActorUserId;

            var cmd = new ToggleOtRule.Command(ruleId, agencyId, actorId);
            var result = await ToggleOtRule.HandleAsync(
                cmd, repo, async c => await db.SaveChangesAsync(c), clock, ct);

            return result.IsSuccess
                ? Results.Ok(new { rule_id = result.Value.RuleId, is_active = result.Value.IsActive })
                : Results.NotFound(new { error = result.Error.Message });
        })
        .WithName("ToggleOtRule")
        .WithSummary("Activa o desactiva una regla OT en hot-swap (AC1)");

        // GET — listar reglas del OT (contrato camelCase para consola frontend)
        group.MapGet("/", async (
            Guid agencyId,
            IOtRuleRepository repo,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanRead(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var rules = await repo.ListByAgencyAsync(agencyId, ct);
            return Results.Ok(rules.Select(MapRuleListItem));
        })
        .WithName("ListOtRules")
        .WithSummary("Lista todas las reglas (activas e inactivas) del OT");
    }

    private static OtRuleListItem MapRuleListItem(OtRule r) =>
        new(
            r.Id,
            r.Name,
            r.TriggerEvent,
            ParseJsonObject(r.ConditionTreeJson),
            ParseJsonArray(r.ActionsJson),
            r.Priority,
            r.IsActive);

    private static JsonElement ParseJsonObject(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? """{"op":"AND","children":[]}""" : json);
        return doc.RootElement.Clone();
    }

    private static JsonElement ParseJsonArray(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        return doc.RootElement.Clone();
    }
}
