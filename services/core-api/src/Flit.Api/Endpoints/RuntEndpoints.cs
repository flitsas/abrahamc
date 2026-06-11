using Flit.Api.Auth;
using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>HU #9690 — consulta vehicular RUNT con contingencia Verifik → Intempo.</summary>
public static class RuntEndpoints
{
    public static void MapRuntEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/runt")
            .WithTags("RUNT")
            .AddEndpointFilter(new TramitesPermissionFilter("modulo.companias.crud-total"));

        group.MapGet("/vehicles/{plate}", async (
            string plate,
            bool simulateRuntUnavailable,
            bool simulateAllProvidersDown,
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
            var tenantId = session.TenantId;
            if (tenantId is null)
            {
                return Results.BadRequest(new { error = "Contexto de tenant requerido." });
            }

            var actorId = session.ActorUserId ?? Guid.Parse("00000000-0000-7000-8000-000000000001");
            var result = await QueryVehicleWithRuntContingency.HandleAsync(
                new QueryVehicleWithRuntContingency.Command(
                    tenantId.Value,
                    plate,
                    ProcedureInstanceId: null,
                    actorId,
                    simulateRuntUnavailable,
                    simulateAllProvidersDown),
                configRepo,
                providers,
                syncLogRepo,
                queryResultsRepo,
                circuitBreaker,
                ct => uow.SaveChangesAsync(ct),
                clock,
                ct);

            return MapRuntVehicleQueryResult(result);
        })
        .WithName("GetRuntVehicleByPlate");
    }

    internal static IResult MapRuntVehicleQueryResult(
        Result<QueryVehicleWithRuntContingency.Response, RuntVehicleQueryError> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var err = result.Error;
        return err.Code switch
        {
            RuntVehicleQueryErrorCode.MissingPlate => Results.BadRequest(new
            {
                code = "MISSING_PLATE",
                message = err.Message,
            }),
            RuntVehicleQueryErrorCode.VehicleNotFound => Results.Json(
                new { code = "VEHICLE_NOT_FOUND", message = err.Message },
                statusCode: StatusCodes.Status404NotFound),
            RuntVehicleQueryErrorCode.AllProvidersUnavailable => Results.Json(
                new { code = "RUNT_UNAVAILABLE", message = err.Message },
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Problem(err.Message),
        };
    }
}
