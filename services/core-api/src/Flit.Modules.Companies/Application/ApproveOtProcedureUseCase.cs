using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Procedures.Domain;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9455 OT-02 — Trámites unificados y colas QX.
/// Stub que demuestra la bifurcación dashboard/QX usando ot_qx_integrations:
///
/// AC1 — Modo Dashboard: transición de estado en FLIT sin callback externo.
///   pendiente → aprobado (o cualquier transición válida) directamente en procedure_instances.
///
/// AC2 — Modo QX: procesa con idempotency_key sin duplicar.
///   Delega a IWebhookEventRepository para verificar si ya fue procesado.
/// </summary>
public static class ApproveOtProcedure
{
    public enum ErrorCode
    {
        OtNotFound,
        ProcedureNotFound,
        InvalidStateTransition,
        IdempotencyConflict,
    }

    public sealed record Command(
        Guid TrafficAgencyId,
        Guid TenantId,
        Guid ProcedureId,
        Guid ActorUserId,
        string IdempotencyKey);

    public sealed record Response(
        Guid ProcedureId,
        string OtMode,
        string NewState,
        bool CallbackDispatched,
        bool WasIdempotent);

    public sealed record ApprovalError(ErrorCode Code, string Message);

    public static async Task<Result<Response, ApprovalError>> HandleAsync(
        Command cmd,
        IOtQxIntegrationRepository qxRepo,
        IProcedureInstanceRepository procedureRepo,
        IWebhookEventRepository webhookRepo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        // Leer configuración QX del OT (null → dashboard por defecto)
        var qxConfig = await qxRepo.GetByTrafficAgencyAsync(cmd.TrafficAgencyId, ct);
        var mode = qxConfig?.Mode ?? OtQxIntegration.Modes.Dashboard;

        // ──────────────────────────────────────────────────────────────────────
        // AC1 — Modo Dashboard: transición directa en FLIT, sin callback externo
        // ──────────────────────────────────────────────────────────────────────
        if (string.Equals(mode, OtQxIntegration.Modes.Dashboard, StringComparison.OrdinalIgnoreCase))
        {
            var procedure = await procedureRepo.GetByIdAsync(cmd.ProcedureId, cmd.TenantId, ct);
            if (procedure is null)
            {
                return Result<Response, ApprovalError>.Failure(
                    new ApprovalError(ErrorCode.ProcedureNotFound,
                        $"Trámite {cmd.ProcedureId} no encontrado para el tenant."));
            }

            var validationError = ProcedureStateTransitionGuard.Validate(
                procedure.State, ProcedureStates.Aprobado);

            if (validationError is not null)
            {
                return Result<Response, ApprovalError>.Failure(
                    new ApprovalError(ErrorCode.InvalidStateTransition,
                        $"Transición inválida {procedure.State} → aprobado."));
            }

            var approved = procedure with
            {
                State = ProcedureStates.Aprobado,
                UpdatedAt = clock.UtcNow,
                UpdatedBy = cmd.ActorUserId,
                RowVersion = procedure.RowVersion + 1,
            };

            await procedureRepo.SaveAsync(approved, ct);
            await saveChanges(ct);

            return Result<Response, ApprovalError>.Success(new Response(
                ProcedureId: procedure.Id,
                OtMode: mode,
                NewState: ProcedureStates.Aprobado,
                CallbackDispatched: false,   // AC1: sin callback externo
                WasIdempotent: false));
        }

        // ──────────────────────────────────────────────────────────────────────
        // AC2 — Modo QX: verificar idempotency_key, no duplicar
        // ──────────────────────────────────────────────────────────────────────
        var alreadyProcessed = await webhookRepo.ExistsByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (alreadyProcessed)
        {
            return Result<Response, ApprovalError>.Success(new Response(
                ProcedureId: cmd.ProcedureId,
                OtMode: mode,
                NewState: "qx_received",
                CallbackDispatched: false,
                WasIdempotent: true));
        }

        // Registrar evento QX inbound con idempotencia
        var now = clock.UtcNow;
        var webhookEvent = WebhookEvent.CreateInbound(
            tenantId: cmd.TenantId,
            trafficAgencyId: cmd.TrafficAgencyId,
            eventType: "qx.procedure.approve",
            payloadJson: $$$"""{"procedure_id":"{{{cmd.ProcedureId}}}","agency_id":"{{{cmd.TrafficAgencyId}}}"}""",
            idempotencyKey: cmd.IdempotencyKey,
            signature: null,
            receivedAt: now);

        webhookEvent.MarkProcessed(now);
        await webhookRepo.AddAsync(webhookEvent, ct);
        await saveChanges(ct);

        return Result<Response, ApprovalError>.Success(new Response(
            ProcedureId: cmd.ProcedureId,
            OtMode: mode,
            NewState: "qx_received",
            CallbackDispatched: false,   // stub: outbound callback diferido
            WasIdempotent: false));
    }
}
