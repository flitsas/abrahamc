using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU #10079 — transición forzada (SuperMaestro) sin guard de máquina de estados.</summary>
public static class ForceProcedureInstanceState
{
    public sealed record Command(
        Guid ProcedureInstanceId,
        Guid TenantId,
        string ToState,
        Guid ChangedByUserId,
        string? Reason);

    public sealed record Response(
        Guid ProcedureInstanceId,
        string FromState,
        string ToState,
        Guid HistoryEntryId);

    public enum ErrorKind
    {
        NotFound,
        InvalidState,
    }

    public sealed record ForceStateError(ErrorKind Kind, string Message);

    public static async Task<Result<Response, ForceStateError>> HandleAsync(
        Command command,
        IProcedureInstanceRepository instanceRepo,
        IProcedureStateHistoryRepository historyRepo,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(command.ProcedureInstanceId, command.TenantId, ct);
        if (instance is null)
        {
            return Result<Response, ForceStateError>.Failure(
                new ForceStateError(ErrorKind.NotFound, "Instancia de trámite no encontrada."));
        }

        var toState = command.ToState.Trim();
        if (string.IsNullOrWhiteSpace(toState))
        {
            return Result<Response, ForceStateError>.Failure(
                new ForceStateError(ErrorKind.InvalidState, "El estado destino es requerido."));
        }

        if (!ProcedureStates.All.Contains(toState))
        {
            return Result<Response, ForceStateError>.Failure(
                new ForceStateError(ErrorKind.InvalidState, $"Estado destino '{toState}' no es válido."));
        }

        var fromState = instance.State;
        var now = DateTimeOffset.UtcNow;
        var updated = instance with
        {
            State = toState,
            UpdatedAt = now,
            UpdatedBy = command.ChangedByUserId,
            RowVersion = instance.RowVersion + 1,
        };

        await instanceRepo.SaveAsync(updated, ct);

        var reason = string.IsNullOrWhiteSpace(command.Reason)
            ? "[FORCE] Transición administrativa"
            : $"[FORCE] {command.Reason.Trim()}";

        var historyId = Guid.NewGuid();
        await historyRepo.AppendAsync(
            new ProcedureStateHistoryEntry(
                historyId,
                command.TenantId,
                command.ProcedureInstanceId,
                fromState,
                toState,
                reason,
                command.ChangedByUserId,
                now),
            ct);

        return Result<Response, ForceStateError>.Success(
            new Response(command.ProcedureInstanceId, fromState, toState, historyId));
    }
}
