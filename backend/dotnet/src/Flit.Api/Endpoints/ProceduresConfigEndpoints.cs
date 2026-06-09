using System.Text.Json;
using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Companies.Ports;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Api.Endpoints;

/// <summary>API RGL-02 (#9438) y RGL-03 (#9439) — reglas y catálogo de endpoints.</summary>
public static class ProceduresConfigEndpoints
{
    public static readonly Guid DefaultActorUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public sealed record EvaluateRulesRequest(
        Guid TenantId,
        Guid ProcedureTypeId,
        Dictionary<string, string?> CapturedFields,
        JsonDocument? ConfigSnapshot = null,
        Guid? ProcedureInstanceId = null);

    public sealed record CreateEndpointRequest(
        Guid TenantId,
        string Code,
        string Name,
        string Url,
        string Method,
        string AuthType,
        JsonElement AuthConfig,
        int TimeoutMs = 5000,
        bool IsActive = true,
        Guid? ActorUserId = null);

    public sealed record UpdateEndpointRequest(
        Guid TenantId,
        string Name,
        string Url,
        string Method,
        string AuthType,
        JsonElement AuthConfig,
        int TimeoutMs,
        bool IsActive,
        int RowVersion,
        Guid? ActorUserId = null);

    public sealed record InvokeEndpointRequest(
        Guid TenantId,
        JsonElement? Payload = null,
        Guid? ProcedureInstanceId = null);

    public sealed record CreateRuleRequest(
        Guid TenantId,
        Guid ProcedureTypeId,
        string Name,
        string? Description,
        JsonElement ConditionTree,
        JsonElement Actions,
        int Priority = 100,
        bool IsActive = true,
        Guid? ActorUserId = null);

    public sealed record UpdateRuleRequest(
        Guid TenantId,
        string Name,
        string? Description,
        JsonElement ConditionTree,
        JsonElement Actions,
        int Priority,
        bool IsActive,
        int RowVersion,
        Guid? ActorUserId = null);

    public static void MapProceduresConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var rules = app.MapGroup("/api/v1/procedures-config/rules")
            .WithTags("Procedures Config - Rules");

        rules.MapPost("/evaluate", async (
            EvaluateRulesRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureRulesRepository rulesRepo,
            IRuleEndpointInvoker endpointInvoker,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var tenantId, out var tenantError))
            {
                return tenantError!;
            }

            var response = await EvaluateProcedureRules.HandleAsync(
                new EvaluateProcedureRules.Query(
                    tenantId,
                    req.ProcedureTypeId,
                    req.CapturedFields,
                    req.ConfigSnapshot,
                    req.ProcedureInstanceId),
                rulesRepo,
                endpointInvoker,
                ct);

            return Results.Ok(new
            {
                matchedRules = response.MatchedRules.Select(m => new
                {
                    ruleId = m.RuleId,
                    ruleName = m.RuleName,
                    priority = m.Priority,
                    actions = m.Actions.Select(a => new { type = a.Type, @params = a.Params }),
                }),
                actions = response.Actions.Select(a => new { type = a.Type, @params = a.Params }),
                endpointInvocations = response.EndpointInvocations.Select(e => new
                {
                    endpointCode = e.EndpointCode,
                    httpStatus = e.HttpStatus,
                    succeeded = e.Succeeded,
                    rateLimited = e.RateLimited,
                    responsePreview = e.ResponsePreview,
                }),
            });
        })
        .RequireTramitesPermission("modulo.tramites.ver")
        .WithName("EvaluateProcedureRules")
        .WithSummary("Evalúa reglas activas (o snapshot) y devuelve acciones");

        rules.MapGet("/", async (
            Guid tenantId,
            Guid procedureTypeId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureRulesCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var items = await ListProcedureRulesCatalog.HandleAsync(
                new ListProcedureRulesCatalog.Query(effectiveTenantId, procedureTypeId), repo, ct);
            return Results.Ok(items);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("ListProcedureRulesCatalog");

        rules.MapGet("/{id:guid}", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureRulesCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var item = await GetProcedureRuleCatalogEntry.HandleAsync(
                new GetProcedureRuleCatalogEntry.Query(effectiveTenantId, id), repo, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("GetProcedureRuleCatalogEntry");

        rules.MapPost("/", async (
            CreateRuleRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureRulesCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var actorId = req.ActorUserId ?? TramitesTenantScope.ResolveActorUserId(session);
            var result = await CreateProcedureRuleCatalogEntry.HandleAsync(
                new CreateProcedureRuleCatalogEntry.Command(
                    effectiveTenantId,
                    req.ProcedureTypeId,
                    req.Name,
                    req.Description,
                    req.ConditionTree,
                    req.Actions,
                    req.Priority,
                    req.IsActive,
                    actorId),
                repo,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/procedures-config/rules/{ok.Id}", ok),
                MapRulesCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("CreateProcedureRuleCatalogEntry");

        rules.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateRuleRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureRulesCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var actorId = req.ActorUserId ?? TramitesTenantScope.ResolveActorUserId(session);
            var result = await UpdateProcedureRuleCatalogEntry.HandleAsync(
                new UpdateProcedureRuleCatalogEntry.Command(
                    effectiveTenantId,
                    id,
                    req.Name,
                    req.Description,
                    req.ConditionTree,
                    req.Actions,
                    req.Priority,
                    req.IsActive,
                    req.RowVersion,
                    actorId),
                repo,
                ct);

            return result.Match(Results.Ok, MapRulesCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("UpdateProcedureRuleCatalogEntry");

        rules.MapDelete("/{id:guid}", async (
            Guid id,
            Guid tenantId,
            Guid? actorUserId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureRulesCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var actorId = actorUserId ?? TramitesTenantScope.ResolveActorUserId(session);
            var result = await DeleteProcedureRuleCatalogEntry.HandleAsync(
                new DeleteProcedureRuleCatalogEntry.Command(effectiveTenantId, id, actorId),
                repo,
                ct);

            return result.Match(_ => Results.NoContent(), MapRulesCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("DeleteProcedureRuleCatalogEntry");

        var endpoints = app.MapGroup("/api/v1/procedures-config/endpoints")
            .WithTags("Procedures Config - Endpoint Catalog");

        endpoints.MapGet("/", async (
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IEndpointCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var items = await ListEndpointCatalog.HandleAsync(
                new ListEndpointCatalog.Query(effectiveTenantId), repo, ct);
            return Results.Ok(items);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("ListEndpointCatalog");

        endpoints.MapGet("/{id:guid}", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IEndpointCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var item = await GetEndpointCatalogEntry.HandleAsync(
                new GetEndpointCatalogEntry.Query(effectiveTenantId, id), repo, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("GetEndpointCatalogEntry");

        endpoints.MapPost("/", async (
            CreateEndpointRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IEndpointCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var actorId = req.ActorUserId ?? TramitesTenantScope.ResolveActorUserId(session);
            var result = await CreateEndpointCatalogEntry.HandleAsync(
                new CreateEndpointCatalogEntry.Command(
                    effectiveTenantId,
                    req.Code,
                    req.Name,
                    req.Url,
                    req.Method,
                    req.AuthType,
                    req.AuthConfig,
                    req.TimeoutMs,
                    req.IsActive,
                    actorId),
                repo,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/procedures-config/endpoints/{ok.Id}", ok),
                MapCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("CreateEndpointCatalogEntry");

        endpoints.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateEndpointRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IEndpointCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var actorId = req.ActorUserId ?? TramitesTenantScope.ResolveActorUserId(session);
            var result = await UpdateEndpointCatalogEntry.HandleAsync(
                new UpdateEndpointCatalogEntry.Command(
                    effectiveTenantId,
                    id,
                    req.Name,
                    req.Url,
                    req.Method,
                    req.AuthType,
                    req.AuthConfig,
                    req.TimeoutMs,
                    req.IsActive,
                    req.RowVersion,
                    actorId),
                repo,
                ct);

            return result.Match(Results.Ok, MapCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("UpdateEndpointCatalogEntry");

        endpoints.MapDelete("/{id:guid}", async (
            Guid id,
            Guid tenantId,
            Guid? actorUserId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IEndpointCatalogRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var actorId = actorUserId ?? TramitesTenantScope.ResolveActorUserId(session);
            var result = await DeleteEndpointCatalogEntry.HandleAsync(
                new DeleteEndpointCatalogEntry.Command(effectiveTenantId, id, actorId),
                repo,
                ct);

            return result.Match(_ => Results.NoContent(), MapCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("DeleteEndpointCatalogEntry");

        endpoints.MapPost("/by-code/{code}/invoke", async (
            string code,
            InvokeEndpointRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IEndpointCatalogRepository catalogRepo,
            IEndpointCallLogRepository callLogRepo,
            EndpointInvocationRateLimiter rateLimiter,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var result = await InvokeCatalogEndpoint.HandleAsync(
                new InvokeCatalogEndpoint.Command(
                    effectiveTenantId, code, req.Payload, req.ProcedureInstanceId),
                catalogRepo,
                callLogRepo,
                rateLimiter,
                httpClientFactory,
                ct);

            return result.Match(Results.Ok, MapCatalogError);
        })
        .RequireTramitesPermission("modulo.tramites.ver")
        .WithName("InvokeCatalogEndpoint");
    }

    private static IResult MapRulesCatalogError(ProcedureRulesCatalogError err) => err.Kind switch
    {
        ProcedureRulesCatalogErrorKind.NotFound => Results.NotFound(new { error = err.Message }),
        ProcedureRulesCatalogErrorKind.Conflict => Results.Conflict(new { error = err.Message }),
        ProcedureRulesCatalogErrorKind.Validation => Results.BadRequest(new { error = err.Message }),
        _ => Results.Problem(err.Message),
    };

    private static IResult MapCatalogError(EndpointCatalogError err) => err.Kind switch
    {
        EndpointCatalogErrorKind.NotFound => Results.NotFound(new { error = err.Message }),
        EndpointCatalogErrorKind.Conflict => Results.Conflict(new { error = err.Message }),
        EndpointCatalogErrorKind.Validation => Results.BadRequest(new { error = err.Message }),
        EndpointCatalogErrorKind.RateLimited => Results.Json(
            new { error = err.Message },
            statusCode: StatusCodes.Status429TooManyRequests),
        _ => Results.Problem(err.Message),
    };
}
