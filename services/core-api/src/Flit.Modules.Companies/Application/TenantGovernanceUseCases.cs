using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

public enum TenantGovernanceErrorCode
{
    InvalidUserId,
    NotFound,
    DuplicateUser,
}

public sealed record TenantGovernanceError(TenantGovernanceErrorCode Code, string Message);

public static class ListTenantUserExceptions
{
    public sealed record Query(Guid TenantId);

    public sealed record ExceptionDto(
        Guid Id,
        Guid UserId,
        string? Reason,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset CreatedAt);

    public static async Task<IReadOnlyList<ExceptionDto>> HandleAsync(
        Query query,
        ITenantGovernanceRepository repo,
        CancellationToken ct = default)
    {
        var rows = await repo.ListUserExceptionsAsync(query.TenantId, ct);
        return rows.Select(r => new ExceptionDto(
            r.Id,
            r.UserId,
            r.Reason,
            r.ExpiresAt,
            r.CreatedAt)).ToList();
    }
}

public static class AddTenantUserExemption
{
    public sealed record Command(
        Guid TenantId,
        Guid UserId,
        string? Reason,
        DateTimeOffset? ExpiresAt,
        Guid ActorUserId);

    public sealed record Response(Guid Id, Guid UserId, string? Reason);

    public static async Task<Result<Response, TenantGovernanceError>> HandleAsync(
        Command cmd,
        ITenantGovernanceRepository repo,
        Func<CancellationToken, Task> saveChanges,
        CancellationToken ct = default)
    {
        if (!await repo.UserBelongsToTenantAsync(cmd.TenantId, cmd.UserId, ct))
        {
            return Result<Response, TenantGovernanceError>.Failure(
                new TenantGovernanceError(
                    TenantGovernanceErrorCode.InvalidUserId,
                    "user_id no pertenece al tenant."));
        }

        var id = await repo.CreateUserExceptionAsync(
            cmd.TenantId,
            cmd.UserId,
            cmd.Reason,
            cmd.ExpiresAt,
            cmd.ActorUserId,
            ct);

        await saveChanges(ct);

        return Result<Response, TenantGovernanceError>.Success(
            new Response(id, cmd.UserId, cmd.Reason));
    }
}

public static class RemoveTenantUserExemption
{
    public sealed record Command(Guid TenantId, Guid ExceptionId, Guid ActorUserId);

    public static async Task<Result<bool, TenantGovernanceError>> HandleAsync(
        Command cmd,
        ITenantGovernanceRepository repo,
        Func<CancellationToken, Task> saveChanges,
        CancellationToken ct = default)
    {
        var existing = await repo.GetUserExceptionByIdAsync(cmd.TenantId, cmd.ExceptionId, ct);
        if (existing is null)
        {
            return Result<bool, TenantGovernanceError>.Failure(
                new TenantGovernanceError(
                    TenantGovernanceErrorCode.NotFound,
                    "Excepción no encontrada."));
        }

        await repo.SoftDeleteUserExceptionAsync(cmd.TenantId, cmd.ExceptionId, cmd.ActorUserId, ct);
        await saveChanges(ct);
        return Result<bool, TenantGovernanceError>.Success(true);
    }
}

public static class ListAuthorizedTrafficAgencies
{
    public sealed record Query(Guid TenantId);

    public sealed record EntryDto(
        Guid Id,
        Guid TrafficAgencyId,
        bool IsEnabled,
        DateTimeOffset UpdatedAt);

    public static async Task<IReadOnlyList<EntryDto>> HandleAsync(
        Query query,
        ITenantGovernanceRepository repo,
        CancellationToken ct = default)
    {
        var rows = await repo.ListAuthorizedTrafficAgenciesAsync(query.TenantId, ct);
        return rows.Select(r => new EntryDto(
            r.Id,
            r.TrafficAgencyId,
            r.IsEnabled,
            r.UpdatedAt)).ToList();
    }
}

public static class UpsertAuthorizedTrafficAgency
{
    public sealed record Command(
        Guid TenantId,
        Guid TrafficAgencyId,
        bool IsEnabled,
        Guid ActorUserId);

    public sealed record Response(Guid TrafficAgencyId, bool IsEnabled);

    public static async Task<Response> HandleAsync(
        Command cmd,
        ITenantGovernanceRepository repo,
        Func<CancellationToken, Task> saveChanges,
        CancellationToken ct = default)
    {
        await repo.UpsertAuthorizedTrafficAgencyAsync(
            cmd.TenantId,
            cmd.TrafficAgencyId,
            cmd.IsEnabled,
            cmd.ActorUserId,
            ct);

        await saveChanges(ct);
        return new Response(cmd.TrafficAgencyId, cmd.IsEnabled);
    }
}
