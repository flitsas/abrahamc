using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU TRA-01 #9433 — transición de estado con guard y historial append-only.</summary>
public static class TransitionProcedureInstance
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
        Guid HistoryEntryId,
        IReadOnlyList<string> AllowedNextStates);

    public enum ErrorKind
    {
        NotFound,
        InvalidTransition,
        InvalidState,
    }

    public sealed record TransitionError(ErrorKind Kind, string Message);

    public static async Task<Result<Response, TransitionError>> HandleAsync(
        Command command,
        IProcedureInstanceRepository instanceRepo,
        IProcedureStateHistoryRepository historyRepo,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(
            command.ProcedureInstanceId, command.TenantId, ct);

        if (instance is null)
        {
            return Result<Response, TransitionError>.Failure(
                new TransitionError(ErrorKind.NotFound, "Instancia de trámite no encontrada."));
        }

        var fromState = instance.State;
        var toState = command.ToState.Trim();

        if (string.IsNullOrWhiteSpace(toState))
        {
            return Result<Response, TransitionError>.Failure(
                new TransitionError(ErrorKind.InvalidState, "El estado destino es requerido."));
        }

        var validationError = ProcedureStateTransitionGuard.Validate(fromState, toState);
        if (validationError is not null)
        {
            return Result<Response, TransitionError>.Failure(
                new TransitionError(ErrorKind.InvalidTransition, validationError.Message));
        }

        var now = DateTimeOffset.UtcNow;
        var updated = instance with
        {
            State = toState,
            UpdatedAt = now,
            UpdatedBy = command.ChangedByUserId,
            RowVersion = instance.RowVersion + 1,
        };

        await instanceRepo.SaveAsync(updated, ct);

        var historyId = Guid.NewGuid();
        await historyRepo.AppendAsync(
            new ProcedureStateHistoryEntry(
                historyId,
                command.TenantId,
                command.ProcedureInstanceId,
                fromState,
                toState,
                command.Reason,
                command.ChangedByUserId,
                now),
            ct);

        var allowedNext = ProcedureStateTransitionGuard.GetAllowedTargets(toState);

        return Result<Response, TransitionError>.Success(
            new Response(
                command.ProcedureInstanceId,
                fromState,
                toState,
                historyId,
                allowedNext));
    }

    public static async Task<Result<IReadOnlyList<string>, TransitionError>> GetAllowedTargetsAsync(
        Guid procedureInstanceId,
        Guid tenantId,
        IProcedureInstanceRepository instanceRepo,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(procedureInstanceId, tenantId, ct);
        if (instance is null)
        {
            return Result<IReadOnlyList<string>, TransitionError>.Failure(
                new TransitionError(ErrorKind.NotFound, "Instancia de trámite no encontrada."));
        }

        return Result<IReadOnlyList<string>, TransitionError>.Success(
            ProcedureStateTransitionGuard.GetAllowedTargets(instance.State));
    }
}
