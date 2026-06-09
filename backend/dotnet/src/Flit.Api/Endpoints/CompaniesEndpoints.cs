using Flit.Api.Auth;
using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Users.Application;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// HU #9445 — Consola de indexación B2B (listar/filtrar, Ver, Editar).
/// Requiere sesión JWT (cookies) y permiso <c>modulo.companias.crud-total</c>.
/// Override opcional <c>X-Flit-Tenant-Id</c> para configs scoped a una compañía (SA).
/// </summary>
public static class CompaniesEndpoints
{
    public sealed record UpdateCompanyIndexRequest(
        string LegalName,
        string? CommercialName,
        string? ModulesEnabledJson);

    public sealed record UpsertModuleConfigRequest(string ConfigJson, bool IsActive = true);

    public sealed record VehicleLookupRequest(
        string Plate,
        Guid? ProcedureInstanceId = null,
        bool SimulateRuntUnavailable = false,
        bool SimulateAllProvidersDown = false);

    public sealed record VehicleOwnershipRuleRequest(
        string Name,
        string RuleType,
        string ConditionJson,
        int Priority = 100,
        bool IsActive = true);

    public sealed record EvaluateVehicleOwnershipRequest(
        string Plate,
        string? ProcedureType = null,
        int? ModelYear = null,
        string? ApprovedExceptionCode = null);

    public static void MapCompaniesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/companies")
            .WithTags("Companies")
            .AddEndpointFilter(new TramitesPermissionFilter("modulo.companias.crud-total"));

        var configs = group.MapGroup("/module-configs").WithTags("Companies - Module Config");

        configs.MapGet("/", async (
            ICompanyModuleConfigsRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var response = await ListCompanyModuleConfigs.HandleAsync(
                new ListCompanyModuleConfigs.Query(tenantId.Value), repo, ct);
            return Results.Ok(response);
        })
        .WithName("ListCompanyModuleConfigs");

        configs.MapGet("/{moduleKey}", async (
            string moduleKey,
            ICompanyModuleConfigsRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var config = await GetCompanyModuleConfig.HandleAsync(
                new GetCompanyModuleConfig.Query(tenantId.Value, moduleKey), repo, ct);
            return config is null ? Results.NotFound() : Results.Ok(config);
        })
        .WithName("GetCompanyModuleConfig");

        configs.MapPut("/{moduleKey}", async (
            string moduleKey,
            UpsertModuleConfigRequest req,
            ICompanyModuleConfigsRepository repo,
            IUnitOfWork uow,
            ICompaniesSessionContext session,
            IClock clock,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var result = await UpsertCompanyModuleConfig.HandleAsync(
                new UpsertCompanyModuleConfig.Command(
                    tenantId.Value,
                    moduleKey,
                    req.ConfigJson,
                    req.IsActive,
                    actorId),
                repo,
                async ct =>
                {
                    await uow.SaveChangesAsync(ct);
                    return 0;
                },
                clock,
                ct);

            return result.Match(
                ok => Results.Ok(ok),
                err => err.Code switch
                {
                    UpsertModuleConfigErrorCode.InvalidModuleKey => Results.BadRequest(err),
                    UpsertModuleConfigErrorCode.InvalidConfigJson => Results.BadRequest(err),
                    _ => Results.Problem(err.Message),
                });
        })
        .WithName("UpsertCompanyModuleConfig");

        group.MapGet("/signature-wallet", async (
            ICompanyModuleConfigsRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var wallet = await GetSignatureWallet.HandleAsync(
                new GetSignatureWallet.Query(tenantId.Value), repo, ct);
            return wallet is null ? Results.NotFound() : Results.Ok(wallet);
        })
        .WithName("GetCompanySignatureWallet");

        var runt = group.MapGroup("/runt").WithTags("Companies - RUNT Contingency");

        runt.MapPost("/vehicle-lookup", async (
            VehicleLookupRequest req,
            ICompanyModuleConfigsRepository configRepo,
            IEnumerable<IRuntVehicleQueryProvider> providers,
            IRuntSyncLogRepository syncLogRepo,
            IProcedureQueryResultsRepository queryResultsRepo,
            IRuntProviderCircuitBreaker circuitBreaker,
            IUnitOfWork uow,
            ICompaniesSessionContext session,
            IClock clock,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var result = await QueryVehicleWithRuntContingency.HandleAsync(
                new QueryVehicleWithRuntContingency.Command(
                    tenantId.Value,
                    req.Plate,
                    req.ProcedureInstanceId,
                    actorId,
                    req.SimulateRuntUnavailable,
                    req.SimulateAllProvidersDown),
                configRepo,
                providers,
                syncLogRepo,
                queryResultsRepo,
                circuitBreaker,
                ct => uow.SaveChangesAsync(ct),
                clock,
                ct);

            if (!result.IsSuccess)
            {
                var err = result.Error;
                return err.Code switch
                {
                    RuntVehicleQueryErrorCode.MissingPlate => Results.BadRequest(err),
                    RuntVehicleQueryErrorCode.AllProvidersUnavailable => Results.Json(
                        err,
                        statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.Problem(err.Message),
                };
            }

            return Results.Ok(result.Value);
        })
        .WithName("RuntVehicleLookup");

        var ownership = group.MapGroup("/vehicle-ownership-rules").WithTags("Companies - Vehicle Ownership");

        ownership.MapGet("/", async (
            IVehicleOwnershipRulesRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });

            var items = await ListVehicleOwnershipRules.HandleAsync(
                new ListVehicleOwnershipRules.Query(tenantId.Value), repo, ct);
            return Results.Ok(items);
        })
        .WithName("ListVehicleOwnershipRules");

        ownership.MapPost("/", async (
            VehicleOwnershipRuleRequest req,
            IVehicleOwnershipRulesRepository repo,
            IUnitOfWork uow,
            ICompaniesSessionContext session,
            IClock clock,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var result = await CreateVehicleOwnershipRule.HandleAsync(
                new CreateVehicleOwnershipRule.Command(
                    tenantId.Value,
                    req.Name,
                    req.RuleType,
                    req.ConditionJson,
                    req.Priority,
                    req.IsActive,
                    actorId),
                repo,
                ct => uow.SaveChangesAsync(ct),
                clock,
                ct);

            return result.Match(
                id => Results.Created($"/api/v1/companies/vehicle-ownership-rules/{id}", new { id }),
                err => err.Code switch
                {
                    UpsertVehicleRuleErrorCode.InvalidRuleType => Results.BadRequest(err),
                    UpsertVehicleRuleErrorCode.InvalidConditionJson => Results.BadRequest(err),
                    _ => Results.Problem(err.Message),
                });
        })
        .WithName("CreateVehicleOwnershipRule");

        ownership.MapPut("/{id:guid}", async (
            Guid id,
            VehicleOwnershipRuleRequest req,
            IVehicleOwnershipRulesRepository repo,
            IUnitOfWork uow,
            ICompaniesSessionContext session,
            IClock clock,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var result = await UpdateVehicleOwnershipRule.HandleAsync(
                new UpdateVehicleOwnershipRule.Command(
                    id,
                    tenantId.Value,
                    req.Name,
                    req.RuleType,
                    req.ConditionJson,
                    req.Priority,
                    req.IsActive,
                    actorId),
                repo,
                ct => uow.SaveChangesAsync(ct),
                clock,
                ct);

            return result.Match(
                ruleId => Results.Ok(new { id = ruleId }),
                err => err.Code switch
                {
                    UpsertVehicleRuleErrorCode.NotFound => Results.NotFound(),
                    UpsertVehicleRuleErrorCode.WrongTenant => Results.Forbid(),
                    UpsertVehicleRuleErrorCode.InvalidRuleType => Results.BadRequest(err),
                    _ => Results.Problem(err.Message),
                });
        })
        .WithName("UpdateVehicleOwnershipRule");

        ownership.MapPost("/evaluate", async (
            EvaluateVehicleOwnershipRequest req,
            IVehicleOwnershipRulesRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenantId(session);
            if (tenantId is null)
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });

            var result = await EvaluateVehicleOwnershipInterceptor.HandleAsync(
                new EvaluateVehicleOwnershipInterceptor.Command(
                    tenantId.Value,
                    req.Plate,
                    req.ProcedureType,
                    req.ModelYear,
                    req.ApprovedExceptionCode),
                repo,
                ct);

            if (!result.IsSuccess)
            {
                var block = result.Error;
                return Results.Json(
                    block,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            return Results.Ok(result.Value);
        })
        .WithName("EvaluateVehicleOwnershipInterceptor");

        group.MapGet("/", async (
            int? page,
            int? limit,
            string? id,
            string? nit,
            string? name,
            DateTimeOffset? createdFrom,
            DateTimeOffset? createdTo,
            ICompaniesIndexRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            if (!session.IsSuperAdmin && session.TenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido para administrador de tenant." });
            }

            Guid? companyId = null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                if (!Guid.TryParse(id.Trim(), out var parsedId))
                {
                    return Results.BadRequest(new { error = "El parámetro id debe ser un UUID válido." });
                }

                companyId = parsedId;
            }

            var response = await ListCompaniesIndex.HandleAsync(
                new ListCompaniesIndex.Query(
                    page ?? 1,
                    limit ?? 20,
                    companyId,
                    nit,
                    name,
                    createdFrom,
                    createdTo),
                repo,
                ct);
            return Results.Ok(response);
        })
        .WithName("ListCompaniesIndex");

        group.MapGet("/{id:guid}", async (
            Guid id,
            ICompaniesIndexRepository repo,
            ICompaniesSessionContext session,
            CancellationToken ct) =>
        {
            if (!session.IsSuperAdmin && session.TenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var company = await GetCompanyIndex.HandleAsync(new GetCompanyIndex.Query(id), repo, ct);
            return company is null ? Results.NotFound() : Results.Ok(company);
        })
        .WithName("GetCompanyIndex");

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateCompanyIndexRequest req,
            ICompaniesRepository companiesRepo,
            ICompaniesIndexRepository indexRepo,
            IUnitOfWork uow,
            ICompaniesSessionContext session,
            IClock clock,
            CancellationToken ct) =>
        {
            if (!session.IsSuperAdmin && session.TenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var result = await UpdateCompanyIndex.HandleAsync(
                new UpdateCompanyIndex.Command(
                    id,
                    req.LegalName,
                    req.CommercialName,
                    req.ModulesEnabledJson ?? "{}",
                    actorId),
                companiesRepo,
                indexRepo,
                async ct =>
                {
                    await uow.SaveChangesAsync(ct);
                    return 0;
                },
                clock,
                ct);

            return result.Match(
                ok => Results.Ok(ok),
                err => err.Code switch
                {
                    UpdateCompanyIndexErrorCode.NotFound => Results.NotFound(),
                    _ => Results.Problem(err.Message),
                });
        })
        .WithName("UpdateCompanyIndex");
    }

    private static Guid? ResolveTenantId(ICompaniesSessionContext session) => session.TenantId;
}
