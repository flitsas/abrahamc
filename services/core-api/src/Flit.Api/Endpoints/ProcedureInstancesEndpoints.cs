using Flit.Modules.Companies.Application;
using IOtRuleRepository = Flit.Modules.Companies.Ports.IOtRuleRepository;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;
using Flit.SharedKernel;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.Modules.Procedures.Application;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;

namespace Flit.Api.Endpoints;

/// <summary>
/// HU TRA-02 #9434 — Radicación de trámite con snapshot inmutable de configuración (ADR-0010).
/// </summary>
public static class ProcedureInstancesEndpoints
{
    public static void MapProcedureInstancesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/procedures/instances")
            .WithTags("Procedures - Runtime");

        // POST /api/v1/procedures/instances
        group.MapPost("/", async (
            CreateProcedureInstanceRequest req,
            IProceduresConfigReadRepository configRepo,
            IProcedureFilingAuditPort auditPort,
            IProcedureInstanceRepository instanceRepo,
            IProcedureFieldValueRepository fieldValueRepo,
            IProcedureActorRepository actorRepo,
            IProcedureVehicleRepository vehicleRepo,
            IDocumentTypesReadRepository documentTypesRepo,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });
            if (req.FiledByUserId == Guid.Empty)
                return Results.BadRequest(new { error = "filedByUserId es requerido." });
            if (string.IsNullOrWhiteSpace(req.ProcedureTypeCode))
                return Results.BadRequest(new { error = "procedureTypeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.EdgeCode))
                return Results.BadRequest(new { error = "edgeCode es requerido." });

            // 1. Resolver configuración global → tenant → OT (ProceduresConfig)
            var config = await GetProcedureConfiguration.HandleAsync(
                new GetProcedureConfiguration.Query(
                    req.TenantId, req.ProcedureTypeCode, req.TrafficAgencyId),
                configRepo, ct);

            if (config is null)
                return Results.NotFound(new
                {
                    error = $"Tipo '{req.ProcedureTypeCode}' no encontrado o no activo para el tenant.",
                    code = "PROCEDURE_TYPE_NOT_FOUND",
                });

            // 2. Validar reglas Leasing/Locatario + construir config_snapshot ADR-0010
            var (filingRecord, filingError) = await RecordProcedureFiling.HandleAsync(
                new RecordProcedureFiling.Command(
                    req.TenantId,
                    req.FiledByUserId,
                    req.ProcedureTypeCode,
                    req.TrafficAgencyId,
                    req.EdgeCode,
                    req.OmittedQueries,
                    config),
                auditPort, ct);

            if (filingError is not null)
                return MapFilingError(filingError);

            // 3. Persistir procedure_instance con snapshot inmutable (TRA-02)
            var (instance, instanceError) = await FileProcedureInstance.HandleAsync(
                new FileProcedureInstance.Command(
                    TenantId: req.TenantId,
                    ProcedureTypeId: config.ProcedureTypeId,
                    TrafficAgencyId: req.TrafficAgencyId,
                    ProcedureTypeCode: req.ProcedureTypeCode,
                    FiledByUserId: req.FiledByUserId,
                    ConfigSnapshotJson: filingRecord!.ConfigSnapshotJson),
                instanceRepo, ct);

            if (instanceError is not null)
                return Results.Problem(instanceError.Message);

            await SaveProcedureFieldValues.HandleAsync(
                req.TenantId,
                instance!.Id,
                req.FiledByUserId,
                edgeRole: null,
                req.FieldValues,
                fieldValueRepo,
                ct);

            var documentTypeCode = string.IsNullOrWhiteSpace(req.DocumentTypeCode) ? "CC" : req.DocumentTypeCode;
            var capture = await SaveProcedureRuntimeCapture.HandleAsync(
                new SaveProcedureRuntimeCapture.Command(
                    req.TenantId,
                    instance.Id,
                    req.FiledByUserId,
                    req.ProcedureTypeCode,
                    req.TrafficAgencyId,
                    req.EdgeCode,
                    documentTypeCode,
                    null,
                    req.FieldValues),
                configRepo,
                documentTypesRepo,
                actorRepo,
                vehicleRepo,
                ct);

            return Results.Created(
                $"/api/v1/procedures/instances/{instance!.Id}",
                new ProcedureInstanceResponse(
                    instance.Id,
                    instance.TenantId,
                    instance.ProcedureTypeId,
                    req.ProcedureTypeCode,
                    instance.TrafficAgencyId,
                    instance.ReferenceNumber,
                    instance.State,
                    instance.ConfigSchemaVersion,
                    instance.CreatedAt,
                    instance.CreatedBy,
                    capture.PrimaryActorId,
                    capture.VehicleId,
                    capture.PrimaryActorEdgeRole));
        })
        .WithName("CreateProcedureInstance")
        .WithSummary("Radica un trámite con snapshot de configuración inmutable (TRA-02 #9434)");

        // GET /api/v1/procedures/instances — listado por tenant (dashboard TRA-04 / #9369)
        group.MapGet("/", async (
            Guid tenantId,
            IProcedureInstanceRepository instanceRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var instances = await instanceRepo.ListByTenantAsync(tenantId, ct);
            return Results.Ok(new
            {
                items = instances.Select(i => new ProcedureInstanceListItem(
                    i.Id,
                    i.ReferenceNumber,
                    i.State,
                    i.ProcedureTypeId,
                    i.TrafficAgencyId,
                    i.RadicatedAt,
                    i.CreatedAt,
                    i.CreatedBy)).ToList(),
            });
        })
        .WithName("ListProcedureInstances")
        .WithSummary("Lista instancias radicadas del tenant (dashboard operador)");

        // GET /api/v1/procedures/instances/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            Guid tenantId,
            IProcedureInstanceRepository instanceRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var instance = await instanceRepo.GetByIdAsync(id, tenantId, ct);
            return instance is null
                ? Results.NotFound(new { error = $"Instancia '{id}' no encontrada." })
                : Results.Ok(instance);
        })
        .WithName("GetProcedureInstance")
        .WithSummary("Obtiene una instancia de trámite por ID");

        // POST /api/v1/procedures/instances/{id}/queries/run — MTR-04 #9428
        group.MapPost("/{id:guid}/queries/run", async (
            Guid id,
            RunProcedureQueriesRequest req,
            IProcedureInstanceRepository instanceRepo,
            IProceduresConfigReadRepository configRepo,
            IDocumentTypesReadRepository documentTypesRepo,
            IExternalQueryProvider queryProvider,
            IExternalQueryCallLogRepository callLogRepo,
            IRuntSyncLogRepository runtSyncLogRepo,
            IProcedureQueryResultRepository queryResultRepo,
            ExternalQueryCircuitBreaker circuitBreaker,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });
            if (req.ExecutedByUserId == Guid.Empty)
                return Results.BadRequest(new { error = "executedByUserId es requerido." });
            if (string.IsNullOrWhiteSpace(req.ProcedureTypeCode))
                return Results.BadRequest(new { error = "procedureTypeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.EdgeCode))
                return Results.BadRequest(new { error = "edgeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.DocumentTypeCode))
                return Results.BadRequest(new { error = "documentTypeCode es requerido." });

            var result = await RunProcedureExternalQueries.HandleAsync(
                new RunProcedureExternalQueries.Command(
                    id,
                    req.TenantId,
                    req.ProcedureTypeCode,
                    req.TrafficAgencyId,
                    req.EdgeCode,
                    req.DocumentTypeCode,
                    req.ExecutedByUserId,
                    req.CapturedFields,
                    req.OmittedConnectorCodes),
                instanceRepo,
                configRepo,
                documentTypesRepo,
                queryProvider,
                callLogRepo,
                runtSyncLogRepo,
                queryResultRepo,
                circuitBreaker,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    procedureInstanceId = ok.ProcedureInstanceId,
                    canContinue = ok.CanContinue,
                    results = ok.Results.Select(r => new
                    {
                        connectorCode = r.ConnectorCode,
                        edgeRole = r.EdgeRole,
                        status = r.Status,
                        succeeded = r.Succeeded,
                        circuitOpen = r.CircuitOpen,
                        mandatory = r.Mandatory,
                        integrationCallId = r.IntegrationCallId,
                        snapshotId = r.SnapshotId,
                    }),
                }),
                err => err.Kind switch
                {
                    RunProcedureExternalQueries.RunQueriesErrorKind.InstanceNotFound =>
                        Results.NotFound(new { error = err.Message }),
                    RunProcedureExternalQueries.RunQueriesErrorKind.InvalidState =>
                        Results.Conflict(new { error = err.Message, code = "INVALID_INSTANCE_STATE" }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .WithName("RunProcedureExternalQueries")
        .WithSummary("Ejecuta consultas externas concurrentes en borrador (MTR-04 #9428)");

        // GET /api/v1/procedures/instances/{id}/query-results — MTR-04 #9428
        group.MapGet("/{id:guid}/query-results", async (
            Guid id,
            Guid tenantId,
            IProcedureInstanceRepository instanceRepo,
            IProcedureQueryResultRepository queryResultRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var result = await GetProcedureQueryResults.HandleAsync(
                new GetProcedureQueryResults.Query(id, tenantId),
                instanceRepo,
                queryResultRepo,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    procedureInstanceId = ok.ProcedureInstanceId,
                    canContinue = ok.CanContinue,
                    results = ok.Results.Select(r => new
                    {
                        connectorCode = r.ConnectorCode,
                        edgeRole = r.EdgeRole,
                        status = r.Status,
                        succeeded = r.Succeeded,
                        circuitOpen = r.CircuitOpen,
                        mandatory = r.Mandatory,
                        integrationCallId = r.IntegrationCallId,
                        snapshotId = r.SnapshotId,
                    }),
                }),
                err => err.Kind switch
                {
                    GetProcedureQueryResults.ErrorKind.InstanceNotFound =>
                        Results.NotFound(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .WithName("GetProcedureQueryResults")
        .WithSummary("Lista snapshots de consultas externas por instancia (MTR-04 #9428)");

        // GET /api/v1/procedures/instances/{id}/allowed-transitions — TRA-01 #9433
        group.MapGet("/{id:guid}/allowed-transitions", async (
            Guid id,
            Guid tenantId,
            IProcedureInstanceRepository instanceRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var result = await TransitionProcedureInstance.GetAllowedTargetsAsync(
                id, tenantId, instanceRepo, ct);

            return result.Match(
                targets => Results.Ok(new { procedureInstanceId = id, allowedTransitions = targets }),
                err => err.Kind switch
                {
                    TransitionProcedureInstance.ErrorKind.NotFound =>
                        Results.NotFound(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .WithName("GetProcedureAllowedTransitions")
        .WithSummary("Lista estados destino permitidos desde el estado actual (TRA-01)");

        // PATCH /api/v1/procedures/instances/{id}/state — TRA-01 #9433
        group.MapPatch("/{id:guid}/state", async (
            Guid id,
            TransitionProcedureStateRequest req,
            IProcedureInstanceRepository instanceRepo,
            IProcedureStateHistoryRepository historyRepo,
            IOtRuleRepository otRulesRepo,
            IClock clock,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });
            if (req.ChangedByUserId == Guid.Empty)
                return Results.BadRequest(new { error = "changedByUserId es requerido." });
            if (string.IsNullOrWhiteSpace(req.ToState))
                return Results.BadRequest(new { error = "toState es requerido." });

            var instance = await instanceRepo.GetByIdAsync(id, req.TenantId, ct);
            if (instance is null)
                return Results.NotFound(new { error = "Instancia de trámite no encontrada." });

            var blockMessage = await OtRuleSubmitGuard.GetBlockMessageAsync(
                instance, req.ToState, otRulesRepo, clock, ct);
            if (blockMessage is not null)
            {
                return Results.Conflict(new { error = blockMessage, code = "OT_RULE_BLOCKED" });
            }

            var result = await TransitionProcedureInstance.HandleAsync(
                new TransitionProcedureInstance.Command(
                    id, req.TenantId, req.ToState, req.ChangedByUserId, req.Reason),
                instanceRepo,
                historyRepo,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    procedureInstanceId = ok.ProcedureInstanceId,
                    fromState = ok.FromState,
                    toState = ok.ToState,
                    historyEntryId = ok.HistoryEntryId,
                    allowedNextStates = ok.AllowedNextStates,
                }),
                err => err.Kind switch
                {
                    TransitionProcedureInstance.ErrorKind.NotFound =>
                        Results.NotFound(new { error = err.Message }),
                    TransitionProcedureInstance.ErrorKind.InvalidTransition =>
                        Results.Conflict(new { error = err.Message, code = "INVALID_STATE_TRANSITION" }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .WithName("TransitionProcedureInstanceState")
        .WithSummary("Transiciona el estado de una instancia con guard y historial (TRA-01 #9433)");

        // GET /api/v1/procedures/instances/{id}/state-history — TRA-01 #9433
        group.MapGet("/{id:guid}/state-history", async (
            Guid id,
            Guid tenantId,
            IProcedureInstanceRepository instanceRepo,
            IProcedureStateHistoryRepository historyRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var instance = await instanceRepo.GetByIdAsync(id, tenantId, ct);
            if (instance is null)
                return Results.NotFound(new { error = $"Instancia '{id}' no encontrada." });

            var entries = await historyRepo.ListByInstanceAsync(tenantId, id, ct);
            return Results.Ok(new
            {
                procedureInstanceId = id,
                currentState = instance.State,
                history = entries.Select(e => new
                {
                    e.Id,
                    e.FromState,
                    e.ToState,
                    e.Reason,
                    e.ChangedBy,
                    e.ChangedAt,
                }),
            });
        })
        .WithName("GetProcedureStateHistory")
        .WithSummary("Historial de transiciones de estado (TRA-01 #9433)");

        // GET /api/v1/procedures/instances/{id}/runtime-capture — TRA-02 #9434
        group.MapGet("/{id:guid}/runtime-capture", async (
            Guid id,
            Guid tenantId,
            IProcedureInstanceRepository instanceRepo,
            IProcedureActorRepository actorRepo,
            IProcedureVehicleRepository vehicleRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var instance = await instanceRepo.GetByIdAsync(id, tenantId, ct);
            if (instance is null)
                return Results.NotFound(new { error = $"Instancia '{id}' no encontrada." });

            var actors = await actorRepo.ListByInstanceAsync(tenantId, id, ct);
            var vehicle = await vehicleRepo.GetByInstanceAsync(tenantId, id, ct);

            return Results.Ok(new
            {
                procedureInstanceId = id,
                state = instance.State,
                actors = actors.Select(a => new
                {
                    id = a.Id,
                    a.EdgeRole,
                    a.PersonKind,
                    a.DocumentTypeCode,
                    a.DocumentNumber,
                    a.FullName,
                }),
                vehicle = vehicle is null
                    ? null
                    : new
                    {
                        id = vehicle.Id,
                        vehicle.VehicleSubkind,
                        vehicle.LicensePlate,
                        vehicle.Vin,
                    },
            });
        })
        .WithName("GetProcedureRuntimeCapture")
        .WithSummary("Actores y vehículo capturados al radicar (TRA-02 #9434)");
    }

    private static IResult MapFilingError(
        Flit.Modules.ProceduresConfig.Domain.ProcedureActorError error) =>
        error.Kind switch
        {
            Flit.Modules.ProceduresConfig.Domain.ProcedureActorErrorKind.MandatoryQueryOmissionForbidden =>
                Results.BadRequest(new { error = error.Message, code = "MANDATORY_QUERY_OMISSION_FORBIDDEN" }),
            Flit.Modules.ProceduresConfig.Domain.ProcedureActorErrorKind.EdgeNotInMatrix =>
                Results.BadRequest(new { error = error.Message, code = "EDGE_NOT_IN_MATRIX" }),
            _ => Results.BadRequest(new { error = error.Message }),
        };

    public sealed record RunProcedureQueriesRequest(
        Guid TenantId,
        Guid ExecutedByUserId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        string DocumentTypeCode,
        Dictionary<string, string?>? CapturedFields,
        List<string>? OmittedConnectorCodes);

    public sealed record TransitionProcedureStateRequest(
        Guid TenantId,
        Guid ChangedByUserId,
        string ToState,
        string? Reason);

    public sealed record CreateProcedureInstanceRequest(
        Guid TenantId,
        Guid FiledByUserId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        List<string>? OmittedQueries,
        Dictionary<string, string?>? FieldValues = null,
        string? DocumentTypeCode = null);

    public sealed record ProcedureInstanceListItem(
        Guid Id,
        string ReferenceNumber,
        string State,
        Guid ProcedureTypeId,
        Guid? TrafficAgencyId,
        DateTimeOffset? RadicatedAt,
        DateTimeOffset CreatedAt,
        Guid CreatedBy);

    public sealed record ProcedureInstanceResponse(
        Guid Id,
        Guid TenantId,
        Guid ProcedureTypeId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string ReferenceNumber,
        string State,
        int ConfigSchemaVersion,
        DateTimeOffset CreatedAt,
        Guid CreatedBy,
        Guid? PrimaryActorId = null,
        Guid? VehicleId = null,
        string? PrimaryActorEdgeRole = null);
}
