using Flit.Api.Auth;
using Flit.Api.Services;
using Flit.Infrastructure.MultiTenant;
using ITenantContext = Flit.Infrastructure.MultiTenant.ITenantContext;
using Flit.Modules.Companies.Application;
using IOtRuleRepository = Flit.Modules.Companies.Ports.IOtRuleRepository;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Integrations.Application;
using IRuntSyncLogRepository = Flit.Modules.Integrations.Ports.IRuntSyncLogRepository;
using Flit.Modules.Integrations.Ports;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.Modules.Procedures.Application;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// HU TRA-02 #9434 + Feature #9732 (#10079/#10080/#10081) — runtime de trámites con RBAC.
/// </summary>
public static class ProcedureInstancesEndpoints
{
    private const string ViewPermission = "modulo.tramites.ver";
    private const string ManagePermission = "modulo.tramites.crud-total";
    private const string SuperMaestroPermission = "tramites.admin.maestro";

    public static void MapProcedureInstancesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/procedures/instances")
            .WithTags("Procedures - Runtime");

        group.MapPost("/", async (
            CreateProcedureInstanceRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProceduresConfigReadRepository configRepo,
            IProcedureFilingAuditPort auditPort,
            IProcedureInstanceRepository instanceRepo,
            IProcedureFieldValueRepository fieldValueRepo,
            IProcedureActorRepository actorRepo,
            IProcedureVehicleRepository vehicleRepo,
            IDocumentTypesReadRepository documentTypesRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var filedByUserId = req.FiledByUserId == Guid.Empty
                ? TramitesTenantScope.ResolveActorUserId(session)
                : req.FiledByUserId;

            if (string.IsNullOrWhiteSpace(req.ProcedureTypeCode))
                return Results.BadRequest(new { error = "procedureTypeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.EdgeCode))
                return Results.BadRequest(new { error = "edgeCode es requerido." });

            var config = await GetProcedureConfiguration.HandleAsync(
                new GetProcedureConfiguration.Query(
                    effectiveTenantId, req.ProcedureTypeCode, req.TrafficAgencyId),
                configRepo, ct);

            if (config is null)
                return Results.NotFound(new
                {
                    error = $"Tipo '{req.ProcedureTypeCode}' no encontrado o no activo para el tenant.",
                    code = "PROCEDURE_TYPE_NOT_FOUND",
                });

            var (filingRecord, filingError) = await RecordProcedureFiling.HandleAsync(
                new RecordProcedureFiling.Command(
                    effectiveTenantId,
                    filedByUserId,
                    req.ProcedureTypeCode,
                    req.TrafficAgencyId,
                    req.EdgeCode,
                    req.OmittedQueries,
                    config),
                auditPort, ct);

            if (filingError is not null)
                return MapFilingError(filingError);

            var (instance, instanceError) = await FileProcedureInstance.HandleAsync(
                new FileProcedureInstance.Command(
                    TenantId: effectiveTenantId,
                    ProcedureTypeId: config.ProcedureTypeId,
                    TrafficAgencyId: req.TrafficAgencyId,
                    ProcedureTypeCode: req.ProcedureTypeCode,
                    FiledByUserId: filedByUserId,
                    ConfigSnapshotJson: filingRecord!.ConfigSnapshotJson),
                instanceRepo, ct);

            if (instanceError is not null)
                return Results.Problem(instanceError.Message);

            await SaveProcedureFieldValues.HandleAsync(
                effectiveTenantId,
                instance!.Id,
                filedByUserId,
                edgeRole: null,
                req.FieldValues,
                fieldValueRepo,
                ct);

            var documentTypeCode = string.IsNullOrWhiteSpace(req.DocumentTypeCode) ? "CC" : req.DocumentTypeCode;
            var capture = await SaveProcedureRuntimeCapture.HandleAsync(
                new SaveProcedureRuntimeCapture.Command(
                    effectiveTenantId,
                    instance.Id,
                    filedByUserId,
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
                $"/api/v1/procedures/instances/{instance.Id}",
                new ProcedureInstanceResponse(
                    instance.Id,
                    instance.TenantId,
                    instance.ProcedureTypeId,
                    req.ProcedureTypeCode,
                    instance.TrafficAgencyId,
                    instance.ReferenceNumber,
                    instance.ReferenceNumber,
                    instance.State,
                    instance.ConfigSchemaVersion,
                    instance.CreatedAt,
                    instance.CreatedBy,
                    capture.PrimaryActorId,
                    capture.VehicleId,
                    capture.PrimaryActorEdgeRole));
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("CreateProcedureInstance")
        .WithSummary("Radica un trámite con snapshot de configuración inmutable (TRA-02 #9434)");

        group.MapGet("/", async (
            Guid tenantId,
            int? page,
            int? pageSize,
            string? state,
            string? procedureTypeCode,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var result = await ListProcedureInstances.HandleAsync(
                new ListProcedureInstances.Query(
                    effectiveTenantId,
                    page ?? 1,
                    pageSize ?? 20,
                    state,
                    procedureTypeCode),
                instanceRepo,
                ct);

            return Results.Ok(new
            {
                items = result.Items.Select(i => new
                {
                    i.Id,
                    compositeId = i.CompositeId,
                    i.ReferenceNumber,
                    i.ProcedureTypeCode,
                    i.State,
                    i.ProcedureTypeId,
                    i.TrafficAgencyId,
                    i.RadicatedAt,
                    i.CreatedAt,
                    i.CreatedBy,
                }),
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize,
            });
        })
        .RequireTramitesPermission(ViewPermission)
        .WithName("ListProcedureInstances")
        .WithSummary("Lista instancias paginadas con ID compuesto (#10079)");

        group.MapGet("/{id:guid}", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var instance = await instanceRepo.GetByIdAsync(id, effectiveTenantId, ct);
            return instance is null
                ? Results.NotFound(new { error = $"Instancia '{id}' no encontrada." })
                : Results.Ok(new
                {
                    instance.Id,
                    compositeId = instance.ReferenceNumber,
                    instance.TenantId,
                    instance.ProcedureTypeId,
                    instance.TrafficAgencyId,
                    instance.ReferenceNumber,
                    instance.State,
                    instance.ConfigSchemaVersion,
                    instance.RadicatedAt,
                    instance.CreatedAt,
                    instance.CreatedBy,
                });
        })
        .RequireTramitesPermission(ViewPermission)
        .WithName("GetProcedureInstance")
        .WithSummary("Obtiene una instancia de trámite por ID");

        group.MapPost("/{id:guid}/queries/run", async (
            Guid id,
            RunProcedureQueriesRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
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
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var executedBy = req.ExecutedByUserId == Guid.Empty
                ? TramitesTenantScope.ResolveActorUserId(session)
                : req.ExecutedByUserId;

            if (string.IsNullOrWhiteSpace(req.ProcedureTypeCode))
                return Results.BadRequest(new { error = "procedureTypeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.EdgeCode))
                return Results.BadRequest(new { error = "edgeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.DocumentTypeCode))
                return Results.BadRequest(new { error = "documentTypeCode es requerido." });

            var result = await RunProcedureExternalQueries.HandleAsync(
                new RunProcedureExternalQueries.Command(
                    id,
                    effectiveTenantId,
                    req.ProcedureTypeCode,
                    req.TrafficAgencyId,
                    req.EdgeCode,
                    req.DocumentTypeCode,
                    executedBy,
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

            return MapRunQueriesResult(id, result);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("RunProcedureExternalQueries")
        .WithSummary("Ejecuta consultas externas concurrentes en borrador (MTR-04 #9428)");

        group.MapPost("/{id:guid}/queries/run-async", async (
            Guid id,
            RunProcedureQueriesRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            ProcedureQueryBackgroundRunner backgroundRunner,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var executedBy = req.ExecutedByUserId == Guid.Empty
                ? TramitesTenantScope.ResolveActorUserId(session)
                : req.ExecutedByUserId;

            if (string.IsNullOrWhiteSpace(req.ProcedureTypeCode))
                return Results.BadRequest(new { error = "procedureTypeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.EdgeCode))
                return Results.BadRequest(new { error = "edgeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.DocumentTypeCode))
                return Results.BadRequest(new { error = "documentTypeCode es requerido." });

            var jobId = backgroundRunner.EnqueueRunAsync(
                new RunProcedureExternalQueries.Command(
                    id,
                    effectiveTenantId,
                    req.ProcedureTypeCode,
                    req.TrafficAgencyId,
                    req.EdgeCode,
                    req.DocumentTypeCode,
                    executedBy,
                    req.CapturedFields,
                    req.OmittedConnectorCodes));

            return Results.Accepted(
                $"/api/v1/procedures/instances/{id}/queries/jobs/{jobId}",
                new { jobId, status = "pending" });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("RunProcedureExternalQueriesAsync")
        .WithSummary("Encola consultas externas en background (#10080)");

        group.MapGet("/{id:guid}/query-results", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureQueryResultRepository queryResultRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var result = await GetProcedureQueryResults.HandleAsync(
                new GetProcedureQueryResults.Query(id, effectiveTenantId),
                instanceRepo,
                queryResultRepo,
                ct);

            return MapQueryResultsResult(result);
        })
        .RequireTramitesPermission(ViewPermission)
        .WithName("GetProcedureQueryResults")
        .WithSummary("Lista snapshots de consultas externas por instancia (MTR-04 #9428)");

        group.MapGet("/{id:guid}/allowed-transitions", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var result = await TransitionProcedureInstance.GetAllowedTargetsAsync(
                id, effectiveTenantId, instanceRepo, ct);

            return result.Match(
                targets => Results.Ok(new { procedureInstanceId = id, allowedTransitions = targets }),
                err => err.Kind switch
                {
                    TransitionProcedureInstance.ErrorKind.NotFound =>
                        Results.NotFound(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .RequireTramitesPermission(ViewPermission)
        .WithName("GetProcedureAllowedTransitions")
        .WithSummary("Lista estados destino permitidos desde el estado actual (TRA-01)");

        group.MapPatch("/{id:guid}/state", async (
            Guid id,
            TransitionProcedureStateRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureStateHistoryRepository historyRepo,
            IOtRuleRepository otRulesRepo,
            IClock clock,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var changedBy = req.ChangedByUserId == Guid.Empty
                ? TramitesTenantScope.ResolveActorUserId(session)
                : req.ChangedByUserId;

            if (string.IsNullOrWhiteSpace(req.ToState))
                return Results.BadRequest(new { error = "toState es requerido." });

            var instance = await instanceRepo.GetByIdAsync(id, effectiveTenantId, ct);
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
                    id, effectiveTenantId, req.ToState, changedBy, req.Reason),
                instanceRepo,
                historyRepo,
                ct);

            return MapTransitionResult(result);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("TransitionProcedureInstanceState")
        .WithSummary("Transiciona el estado de una instancia con guard y historial (TRA-01 #9433)");

        group.MapPatch("/{id:guid}/state/force", async (
            Guid id,
            TransitionProcedureStateRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureStateHistoryRepository historyRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var changedBy = req.ChangedByUserId == Guid.Empty
                ? TramitesTenantScope.ResolveActorUserId(session)
                : req.ChangedByUserId;

            if (string.IsNullOrWhiteSpace(req.ToState))
                return Results.BadRequest(new { error = "toState es requerido." });

            var result = await ForceProcedureInstanceState.HandleAsync(
                new ForceProcedureInstanceState.Command(
                    id, effectiveTenantId, req.ToState, changedBy, req.Reason),
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
                    forced = true,
                }),
                err => err.Kind switch
                {
                    ForceProcedureInstanceState.ErrorKind.NotFound =>
                        Results.NotFound(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .RequireTramitesPermission(SuperMaestroPermission)
        .WithName("ForceProcedureInstanceState")
        .WithSummary("Transición forzada SuperMaestro sin guard (#10079)");

        group.MapGet("/{id:guid}/state-history", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureStateHistoryRepository historyRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var instance = await instanceRepo.GetByIdAsync(id, effectiveTenantId, ct);
            if (instance is null)
                return Results.NotFound(new { error = $"Instancia '{id}' no encontrada." });

            var entries = await historyRepo.ListByInstanceAsync(effectiveTenantId, id, ct);
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
        .RequireTramitesPermission(ViewPermission)
        .WithName("GetProcedureStateHistory")
        .WithSummary("Historial de transiciones de estado (TRA-01 #9433)");

        group.MapPost("/{id:guid}/owners", async (
            Guid id,
            SaveProcedureOwnersRequest req,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureActorRepository actorRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, req.TenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var savedBy = req.SavedByUserId == Guid.Empty
                ? TramitesTenantScope.ResolveActorUserId(session)
                : req.SavedByUserId;

            var result = await SaveProcedureOwners.HandleAsync(
                new SaveProcedureOwners.Command(
                    id,
                    effectiveTenantId,
                    savedBy,
                    req.Owners.Select(o => new SaveProcedureOwners.OwnerInput(
                        o.DocumentTypeCode,
                        o.DocumentNumber,
                        o.FullName,
                        o.OwnershipPercentage,
                        o.OwnerSequence)).ToList()),
                instanceRepo,
                actorRepo,
                ct);

            return result.Match(
                ok => Results.Ok(new { procedureInstanceId = ok.ProcedureInstanceId, ownerCount = ok.OwnerCount }),
                err => err.Kind switch
                {
                    SaveProcedureOwners.ErrorKind.InstanceNotFound =>
                        Results.NotFound(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("SaveProcedureOwners")
        .WithSummary("Registra copropietarios con porcentaje (#10081)");

        group.MapGet("/{id:guid}/ownership/validate", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureActorRepository actorRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var result = await ValidateProcedureOwnership.HandleAsync(
                new ValidateProcedureOwnership.Query(id, effectiveTenantId),
                instanceRepo,
                actorRepo,
                ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    isValid = ok.IsValid,
                    totalPercentage = ok.TotalPercentage,
                    owners = ok.Owners,
                    errors = ok.Errors,
                }),
                err => err.Kind switch
                {
                    ValidateProcedureOwnership.ErrorKind.InstanceNotFound =>
                        Results.NotFound(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                });
        })
        .RequireTramitesPermission(ViewPermission)
        .WithName("ValidateProcedureOwnership")
        .WithSummary("Valida suma 100% de copropiedad (#10081)");

        group.MapGet("/{id:guid}/runtime-capture", async (
            Guid id,
            Guid tenantId,
            ITenantContext tenantContext,
            ICompaniesSessionContext session,
            IProcedureInstanceRepository instanceRepo,
            IProcedureActorRepository actorRepo,
            IProcedureVehicleRepository vehicleRepo,
            CancellationToken ct) =>
        {
            if (!TramitesTenantScope.TryResolve(
                    tenantContext, session, tenantId, out var effectiveTenantId, out var tenantError))
            {
                return tenantError!;
            }

            var instance = await instanceRepo.GetByIdAsync(id, effectiveTenantId, ct);
            if (instance is null)
                return Results.NotFound(new { error = $"Instancia '{id}' no encontrada." });

            var actors = await actorRepo.ListByInstanceAsync(effectiveTenantId, id, ct);
            var vehicle = await vehicleRepo.GetByInstanceAsync(effectiveTenantId, id, ct);

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
                    a.OwnershipPercentage,
                    a.OwnerSequence,
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
        .RequireTramitesPermission(ViewPermission)
        .WithName("GetProcedureRuntimeCapture")
        .WithSummary("Actores y vehículo capturados al radicar (TRA-02 #9434)");
    }

    private static IResult MapRunQueriesResult(
        Guid id,
        Result<RunProcedureExternalQueries.Response, RunProcedureExternalQueries.RunQueriesError> result) =>
        result.Match(
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

    private static IResult MapQueryResultsResult(
        Result<GetProcedureQueryResults.Response, GetProcedureQueryResults.QueryError> result) =>
        result.Match(
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

    private static IResult MapTransitionResult(
        Result<TransitionProcedureInstance.Response, TransitionProcedureInstance.TransitionError> result) =>
        result.Match(
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

    public sealed record SaveProcedureOwnersRequest(
        Guid TenantId,
        Guid SavedByUserId,
        List<OwnerInputDto> Owners);

    public sealed record OwnerInputDto(
        string DocumentTypeCode,
        string DocumentNumber,
        string? FullName,
        decimal? OwnershipPercentage,
        short OwnerSequence);

    public sealed record CreateProcedureInstanceRequest(
        Guid TenantId,
        Guid FiledByUserId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        List<string>? OmittedQueries,
        Dictionary<string, string?>? FieldValues = null,
        string? DocumentTypeCode = null);

    public sealed record ProcedureInstanceResponse(
        Guid Id,
        Guid TenantId,
        Guid ProcedureTypeId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string ReferenceNumber,
        string CompositeId,
        string State,
        int ConfigSchemaVersion,
        DateTimeOffset CreatedAt,
        Guid CreatedBy,
        Guid? PrimaryActorId = null,
        Guid? VehicleId = null,
        string? PrimaryActorEdgeRole = null);
}
