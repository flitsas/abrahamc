using Flit.Api.Auth;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Application;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Identity.Ports;
using Flit.Modules.Procedures.Domain;
using Flit.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Flit.Api.Endpoints;

/// <summary>
/// HU #9455 OT-02 — Trámites unificados y colas QX.
/// HU #9457 OT-04 — Consola OT: GET /api/v1/ot/agencies (selector de OT).
/// POST /api/v1/ot/agencies/{agencyId}/procedures/{procedureId}/approve
///   • Modo Dashboard: transición directa pendiente→aprobado en FLIT, sin callback externo (AC1).
///   • Modo QX: registra idempotency_key en webhook_events sin duplicar (AC2).
/// </summary>
public static class OtEndpoints
{
    public sealed record AgencyListItem(
        Guid Id,
        string Code,
        string Name,
        string? City,
        string? Department,
        string Mode,
        bool IsActive);

    // DTO interno para SqlQuery — columnas en snake_case del resultado SQL
    private sealed class AgencyRow
    {
        public Guid id { get; init; }
        public string code { get; init; } = "";
        public string name { get; init; } = "";
        public string? city { get; init; }
        public string? department { get; init; }
        public string mode { get; init; } = "dashboard";
        public bool is_active { get; init; }
    }

    public sealed record OtUserPermissionResponse(
        Guid UserId,
        string Role,
        bool CanAccessParametrizacion);

    public sealed record ApproveProcedureRequest(
        string IdempotencyKey,
        Guid ActorUserId);

    private const string OtOperatorPermission = "modulo.ot.operador";
    private const string OtSuperAdminPermission = "modulo.ot.superadmin";

    public static void MapOtEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ot")
            .WithTags("OT — Organismos de Tránsito");

        // GET /api/v1/ot/agencies?q=bogota&page=1&pageSize=20
        group.MapGet("/agencies", async (
            FlitDbContext db,
            string? q,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.Database
                .SqlQuery<AgencyRow>($"""
                    SELECT
                        a.id,
                        a.code,
                        a.name,
                        a.municipality_name AS city,
                        a.department_name   AS department,
                        COALESCE(q.mode, 'dashboard') AS mode,
                        a.is_active
                    FROM ot.traffic_agencies a
                    LEFT JOIN ot.ot_qx_integrations q ON q.traffic_agency_id = a.id AND q.is_active = true
                    WHERE a.is_active = true
                    """);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim().ToLower()}%";
                query = db.Database.SqlQuery<AgencyRow>($"""
                    SELECT
                        a.id,
                        a.code,
                        a.name,
                        a.municipality_name AS city,
                        a.department_name   AS department,
                        COALESCE(q.mode, 'dashboard') AS mode,
                        a.is_active
                    FROM ot.traffic_agencies a
                    LEFT JOIN ot.ot_qx_integrations q ON q.traffic_agency_id = a.id AND q.is_active = true
                    WHERE a.is_active = true
                      AND (LOWER(a.name) LIKE {term} OR LOWER(a.code) LIKE {term}
                           OR LOWER(a.municipality_name) LIKE {term})
                    """);
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(x => x.name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                total,
                page,
                pageSize,
                items = items.Select(a => new AgencyListItem(
                    a.id, a.code, a.name, a.city, a.department, a.mode, a.is_active))
            });
        })
        .WithName("ListOtAgencies")
        .WithSummary("Lista paginada de Organismos de Tránsito con modo QX/dashboard");

        // GET /api/v1/ot/agencies/{agencyId}
        group.MapGet("/agencies/{agencyId:guid}", async (
            Guid agencyId,
            FlitDbContext db,
            CancellationToken ct) =>
        {
            var items = await db.Database.SqlQuery<AgencyRow>($"""
                SELECT
                    a.id,
                    a.code,
                    a.name,
                    a.municipality_name AS city,
                    a.department_name   AS department,
                    COALESCE(q.mode, 'dashboard') AS mode,
                    a.is_active
                FROM ot.traffic_agencies a
                LEFT JOIN ot.ot_qx_integrations q ON q.traffic_agency_id = a.id AND q.is_active = true
                WHERE a.id = {agencyId}
                """).ToListAsync(ct);

            var a = items.FirstOrDefault();
            if (a is null) return Results.NotFound();

            return Results.Ok(new AgencyListItem(
                a.id, a.code, a.name, a.city, a.department, a.mode, a.is_active));
        })
        .WithName("GetOtAgency")
        .WithSummary("Obtiene un Organismo de Tránsito por ID");

        // GET /api/v1/ot/agencies/{agencyId}/my-permissions
        group.MapGet("/agencies/{agencyId:guid}/my-permissions", (
            Guid agencyId,
            ICompaniesSessionContext session,
            ITokenIssuer tokenIssuer,
            HttpContext ctx) =>
        {
            _ = agencyId;

            var userId = session.ActorUserId;
            if (userId is null)
                return Results.Unauthorized();

            var permissionSlugs = ResolvePermissionSlugs(ctx, tokenIssuer, session);
            var isSuperAdmin = session.IsSuperAdmin
                || permissionSlugs.Contains(OtSuperAdminPermission);
            var isOperator = isSuperAdmin
                || permissionSlugs.Contains(OtOperatorPermission);

            if (!isOperator)
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var role = isSuperAdmin ? "superadmin" : "operador";
            return Results.Ok(new OtUserPermissionResponse(userId.Value, role, isSuperAdmin));
        })
        .WithName("GetOtUserPermissions")
        .WithSummary("Permisos OT del usuario autenticado para la consola");

        group.MapPost("/agencies/{agencyId:guid}/procedures/{procedureId:guid}/approve",
            async (
                Guid agencyId,
                Guid procedureId,
                ApproveProcedureRequest req,
                IOtQxIntegrationRepository qxRepo,
                IProcedureInstanceRepository procedureRepo,
                IWebhookEventRepository webhookRepo,
                FlitDbContext db,
                IClock clock,
                HttpContext ctx,
                CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.IdempotencyKey))
                return Results.BadRequest(new { error = "idempotency_key es requerido." });

            if (!ctx.Request.Headers.TryGetValue("X-Flit-Tenant-Id", out var tenantHeader)
                || !Guid.TryParse(tenantHeader, out var tenantId))
            {
                return Results.BadRequest(new { error = "X-Flit-Tenant-Id requerido." });
            }

            var cmd = new ApproveOtProcedure.Command(
                TrafficAgencyId: agencyId,
                TenantId: tenantId,
                ProcedureId: procedureId,
                ActorUserId: req.ActorUserId,
                IdempotencyKey: req.IdempotencyKey);

            var result = await ApproveOtProcedure.HandleAsync(
                cmd,
                qxRepo,
                procedureRepo,
                webhookRepo,
                async cancellationToken => await db.SaveChangesAsync(cancellationToken),
                clock,
                ct);

            if (!result.IsSuccess)
            {
                var err = result.Error;
                return err.Code switch
                {
                    ApproveOtProcedure.ErrorCode.ProcedureNotFound =>
                        Results.NotFound(new { error = err.Message }),
                    ApproveOtProcedure.ErrorCode.InvalidStateTransition =>
                        Results.UnprocessableEntity(new { error = err.Message }),
                    _ => Results.BadRequest(new { error = err.Message }),
                };
            }

            var r = result.Value;
            return Results.Ok(new
            {
                procedure_id = r.ProcedureId,
                ot_mode = r.OtMode,
                new_state = r.NewState,
                callback_dispatched = r.CallbackDispatched,
                idempotent = r.WasIdempotent,
            });
        })
        .WithName("ApproveOtProcedure")
        .WithSummary("Aprueba un trámite OT en modo Dashboard (AC1) o QX con idempotencia (AC2)");

        // GET /api/v1/ot/agencies/{agencyId}/dashboard — HU #9697 AC1
        group.MapGet("/agencies/{agencyId:guid}/dashboard", async (
            Guid agencyId,
            IOtDashboardRepository dashboardRepo,
            IOtQxIntegrationRepository qxRepo,
            ITokenIssuer tokenIssuer,
            ICompaniesSessionContext session,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanRead(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var result = await GetOtDashboard.HandleAsync(agencyId, dashboardRepo, qxRepo, ct);
            if (!result.IsSuccess)
                return Results.UnprocessableEntity(new { error = result.Error });

            var r = result.Value;
            var apiMode = string.Equals(r.IntegrationMode, "qx", StringComparison.OrdinalIgnoreCase)
                ? "quipux"
                : r.IntegrationMode;

            return Results.Ok(new
            {
                traffic_agency_id = r.TrafficAgencyId,
                integration_mode = apiMode,
                total_procedures = r.TotalProcedures,
                by_state = r.ByState.Select(s => new { state = s.State, count = s.Count }),
                recent_procedures = r.RecentProcedures.Select(p => new
                {
                    id = p.Id,
                    reference_number = p.ReferenceNumber,
                    state = p.State,
                    radicated_at = p.RadicatedAt,
                }),
            });
        })
        .WithName("GetOtDashboard")
        .WithSummary("Dashboard unificado OT — métricas desde BD FLIT (HU #9697)");

        // PATCH /api/v1/ot/agencies/{agencyId}/integration-mode — HU #9697 AC2-AC4
        group.MapPatch("/agencies/{agencyId:guid}/integration-mode", async (
            Guid agencyId,
            IntegrationModeRequest req,
            IOtQxIntegrationRepository qxRepo,
            FlitDbContext db,
            IClock clock,
            ITokenIssuer tokenIssuer,
            ICompaniesSessionContext session,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!OtEndpointAuth.CanAdminister(ctx, tokenIssuer, session))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(req.Mode))
                return Results.BadRequest(new { error = "mode es requerido.", valid_modes = ValidIntegrationModes });

            var actorId = session.ActorUserId ?? DefaultActorUserId;
            var cmd = new UpdateOtIntegrationMode.Command(agencyId, req.Mode, actorId);
            var result = await UpdateOtIntegrationMode.HandleAsync(
                cmd,
                qxRepo,
                async cancellationToken => await db.SaveChangesAsync(cancellationToken),
                clock,
                ct);

            return result.IsSuccess
                ? Results.Ok(new { traffic_agency_id = result.Value.TrafficAgencyId, mode = result.Value.Mode })
                : Results.BadRequest(new { error = result.Error, valid_modes = ValidIntegrationModes });
        })
        .WithName("UpdateOtIntegrationMode")
        .WithSummary("Hot-swap modo Dashboard/QX del OT (HU #9697)");
    }

    public sealed record IntegrationModeRequest(string Mode);

    private static readonly Guid DefaultActorUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly string[] ValidIntegrationModes = ["dashboard", "quipux"];

    private static HashSet<string> ResolvePermissionSlugs(
        HttpContext ctx,
        ITokenIssuer tokenIssuer,
        ICompaniesSessionContext session)
    {
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cookie = ctx.Request.Cookies[AuthCookieNames.Access];
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            var subject = tokenIssuer.ValidateTramitesAccess(cookie);
            if (subject is not null)
            {
                foreach (var slug in subject.PermissionSlugs)
                    slugs.Add(slug);
                return slugs;
            }
        }

        if (session.IsSuperAdmin)
            slugs.Add(OtSuperAdminPermission);

        return slugs;
    }
}
