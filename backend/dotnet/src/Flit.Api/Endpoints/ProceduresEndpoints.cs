using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Companies.Ports;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Api.Endpoints;

/// <summary>HU #9426 — API de configuración de trámites (MTR-02).</summary>
public static class ProceduresEndpoints
{
    public static void MapProceduresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/procedures")
            .WithTags("Procedures - Configuration");

        group.MapGet("/types", async (
            Guid tenantId,
            Guid? trafficAgencyId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProceduresConfigReadRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var types = await ListActiveProcedureTypes.HandleAsync(
                new ListActiveProcedureTypes.Query(effectiveTenantId, trafficAgencyId),
                repo,
                ct);

            return Results.Ok(new { items = types });
        })
        .RequireTramitesPermission("modulo.tramites.ver")
        .WithName("ListActiveProcedureTypes")
        .WithSummary("Lista tipos de trámite con is_active efectivo para tenant/OT");

        group.MapGet("/types/{code}/configuration", async (
            string code,
            Guid tenantId,
            Guid? trafficAgencyId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProceduresConfigReadRepository repo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var config = await GetProcedureConfiguration.HandleAsync(
                new GetProcedureConfiguration.Query(effectiveTenantId, code, trafficAgencyId),
                repo,
                ct);

            return config is null
                ? Results.NotFound(new { error = $"Tipo '{code}' no encontrado o no activo para el tenant." })
                : Results.Ok(config);
        })
        .RequireTramitesPermission("modulo.tramites.ver")
        .WithName("GetProcedureConfiguration")
        .WithSummary("Configuración resuelta global→tenant→OT");

        group.MapPost("/types/{code}/actors/incorporate", async (
            string code,
            IncorporateActorRequest req,
            Guid tenantId,
            Guid? trafficAgencyId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProceduresConfigReadRepository configRepo,
            IDocumentTypesReadRepository documentTypesRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var (ok, error) = await IncorporateProcedureActors.HandleAsync(
                new IncorporateProcedureActors.Command(
                    effectiveTenantId,
                    code,
                    trafficAgencyId,
                    req.EdgeCode,
                    req.DocumentTypeCode,
                    req.CapturedFields),
                configRepo,
                documentTypesRepo,
                ct);

            return error is not null ? MapActorError(error) : Results.Ok(ok);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("IncorporateProcedureActors")
        .WithSummary("Incorpora actor y devuelve consultas a ejecutar (MTR-03)");

        group.MapPost("/types/{code}/file", async (
            string code,
            FileProcedureRequest req,
            Guid tenantId,
            Guid? trafficAgencyId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProceduresConfigReadRepository configRepo,
            IProcedureFilingAuditPort auditPort,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var config = await GetProcedureConfiguration.HandleAsync(
                new GetProcedureConfiguration.Query(effectiveTenantId, code, trafficAgencyId),
                configRepo,
                ct);

            if (config is null)
            {
                return Results.NotFound();
            }

            var filedBy = req.FiledByUserId != Guid.Empty
                ? req.FiledByUserId
                : TramitesTenantScope.ResolveActorUserId(session);

            var (record, error) = await RecordProcedureFiling.HandleAsync(
                new RecordProcedureFiling.Command(
                    effectiveTenantId,
                    filedBy,
                    code,
                    trafficAgencyId,
                    req.EdgeCode,
                    req.OmittedQueries,
                    config),
                auditPort,
                ct);

            return error is not null
                ? MapActorError(error)
                : Results.Created($"/api/v1/procedures/types/{code}/file/{record!.Id}", record);
        })
        .RequireTramitesPermission("modulo.tramites.crud-total")
        .WithName("RecordProcedureFiling")
        .WithSummary("Radicación con omisiones auditadas (MTR-05)");
    }

    private static IResult MapActorError(Flit.Modules.ProceduresConfig.Domain.ProcedureActorError error) =>
        error.Kind switch
        {
            Flit.Modules.ProceduresConfig.Domain.ProcedureActorErrorKind.EdgeNotInMatrix =>
                Results.BadRequest(new { error = error.Message, code = "EDGE_NOT_IN_MATRIX" }),
            Flit.Modules.ProceduresConfig.Domain.ProcedureActorErrorKind.UnknownDocumentType =>
                Results.BadRequest(new { error = error.Message }),
            Flit.Modules.ProceduresConfig.Domain.ProcedureActorErrorKind.InvalidVehicleIdentification =>
                Results.BadRequest(new { error = error.Message }),
            Flit.Modules.ProceduresConfig.Domain.ProcedureActorErrorKind.MandatoryQueryOmissionForbidden =>
                Results.BadRequest(new { error = error.Message }),
            _ => Results.Problem(error.Message),
        };

    public sealed record IncorporateActorRequest(
        string EdgeCode,
        string DocumentTypeCode,
        Dictionary<string, string?>? CapturedFields);

    public sealed record FileProcedureRequest(
        Guid FiledByUserId,
        string EdgeCode,
        List<string>? OmittedQueries);
}
