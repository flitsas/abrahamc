using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Procedures.Application;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9459 INT-02 + #9698 — Webhooks QX con firma mock, logs e hot-update de trámite.
/// </summary>
public static class ReceiveQxWebhook
{
    public sealed record Command(
        Guid TenantId,
        Guid? TrafficAgencyId,
        string EventType,
        string PayloadJson,
        string IdempotencyKey,
        string? Signature);

    public sealed record Response(
        Guid? EventId,
        string Direction,
        string PayloadJson,
        DateTimeOffset? ProcessedAt,
        bool WasIdempotent,
        bool ProcedureUpdated,
        Guid? ProcedureId,
        string? NewState);

    public enum ErrorCode { InvalidSignature }

    public sealed record WebhookError(ErrorCode Code, string Message);

    public static async Task<Result<Response, WebhookError>> HandleAsync(
        Command cmd,
        IQuipuxWebhookAdapter adapter,
        IWebhookEventRepository webhookRepo,
        IIntegrationLogRepository logRepo,
        IProcedureInstanceRepository procedureRepo,
        IProcedureStateHistoryRepository historyRepo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var started = clock.UtcNow;

        if (!adapter.ValidateSignature(cmd.Signature, cmd.PayloadJson))
        {
            await logRepo.AddAsync(new IntegrationLogEntry(
                Guid.CreateVersion7(),
                cmd.TenantId,
                cmd.TrafficAgencyId,
                "quipux_mock",
                WebhookEvent.Directions.Inbound,
                cmd.EventType,
                cmd.PayloadJson,
                401,
                "invalid_signature",
                (int)(clock.UtcNow - started).TotalMilliseconds,
                clock.UtcNow), ct);
            await saveChanges(ct);

            return Result<Response, WebhookError>.Failure(
                new WebhookError(ErrorCode.InvalidSignature, "Firma Quipux inválida."));
        }

        var alreadyExists = await webhookRepo.ExistsByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (alreadyExists)
        {
            return Result<Response, WebhookError>.Success(
                new Response(null, WebhookEvent.Directions.Inbound, cmd.PayloadJson, null, true, false, null, null));
        }

        var mapped = adapter.ParseAndMap(cmd.EventType, cmd.PayloadJson);
        var procedureUpdated = false;
        string? newState = null;

        if (mapped.ProcedureUpdated && mapped.ProcedureId is not null && mapped.NewState is not null)
        {
            var instance = await procedureRepo.GetByIdAsync(mapped.ProcedureId.Value, cmd.TenantId, ct);
            if (instance is not null &&
                ProcedureStateTransitionGuard.CanTransition(instance.State, mapped.NewState))
            {
                var transition = await TransitionProcedureInstance.HandleAsync(
                    new TransitionProcedureInstance.Command(
                        mapped.ProcedureId.Value,
                        cmd.TenantId,
                        mapped.NewState,
                        Guid.Parse("00000000-0000-7000-8000-000000000001"),
                        "Webhook Quipux mock"),
                    procedureRepo,
                    historyRepo,
                    ct);

                if (transition.IsSuccess)
                {
                    procedureUpdated = true;
                    newState = transition.Value.ToState;
                }
            }
        }

        var now = clock.UtcNow;
        var entry = WebhookEvent.CreateInbound(
            cmd.TenantId,
            cmd.TrafficAgencyId,
            cmd.EventType,
            cmd.PayloadJson,
            cmd.IdempotencyKey,
            cmd.Signature,
            now);
        entry.MarkProcessed(now);
        await webhookRepo.AddAsync(entry, ct);

        await logRepo.AddAsync(new IntegrationLogEntry(
            entry.Id,
            cmd.TenantId,
            cmd.TrafficAgencyId,
            "quipux_mock",
            WebhookEvent.Directions.Inbound,
            cmd.EventType,
            cmd.PayloadJson,
            200,
            mapped.ResultMessage,
            (int)(now - started).TotalMilliseconds,
            now), ct);

        await saveChanges(ct);

        return Result<Response, WebhookError>.Success(
            new Response(
                entry.Id,
                entry.Direction,
                entry.PayloadJson,
                entry.ProcessedAt,
                false,
                procedureUpdated,
                mapped.ProcedureId,
                newState));
    }
}
