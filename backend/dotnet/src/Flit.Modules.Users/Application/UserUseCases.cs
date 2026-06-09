using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Users.Application;

// =============================================================================
// GetUser — read por id
// =============================================================================
public static class GetUser
{
    public sealed record Query(Guid Id);

    public sealed record Response(
        Guid Id,
        string Email,
        string FullName,
        string? DocumentType,
        string? DocumentNumber,
        string? Phone,
        UserStatus Status,
        bool MfaEnabled,
        DateTimeOffset? LastLoginAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public static async Task<Response?> HandleAsync(
        Query query, IUsersRepository repo, CancellationToken ct = default)
    {
        var user = await repo.GetByIdAsync(query.Id, ct);
        if (user is null || user.DeletedAt is not null) return null;
        return Map(user);
    }

    internal static Response Map(User u) => new(
        Id: u.Id,
        Email: u.Email,
        FullName: u.FullName,
        DocumentType: u.DocumentType,
        DocumentNumber: u.DocumentNumber,
        Phone: u.Phone,
        Status: u.Status,
        MfaEnabled: u.MfaEnabled,
        LastLoginAt: u.LastLoginAt,
        CreatedAt: u.CreatedAt,
        UpdatedAt: u.UpdatedAt);
}

// =============================================================================
// ListUsers — paginado con search
// =============================================================================
public static class ListUsers
{
    public sealed record Query(int Page, int Limit, string? Search);

    public sealed record Response(
        IReadOnlyList<GetUser.Response> Data,
        int Total,
        int Page,
        int Limit);

    public static async Task<Response> HandleAsync(
        Query query, IUsersRepository repo, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var limit = Math.Clamp(query.Limit, 1, 200);

        var users = await repo.ListAsync(page, limit, query.Search, ct);
        var total = await repo.CountAsync(query.Search, ct);

        return new Response(
            Data: [.. users.Select(GetUser.Map)],
            Total: total,
            Page: page,
            Limit: limit);
    }
}

// =============================================================================
// UpdateUser — actualizar perfil
// =============================================================================
public static class UpdateUser
{
    public sealed record Command(
        Guid Id,
        string FullName,
        string? DocumentType,
        string? DocumentNumber,
        string? Phone);

    public abstract record UpdateUserError(string Code, string Message)
    {
        public sealed record NotFound() : UpdateUserError("USER_NOT_FOUND", "Usuario no encontrado");
        public sealed record ValidationFailed(string Detail) : UpdateUserError("VALIDATION_FAILED", Detail);
    }

    public static async Task<Result<GetUser.Response, UpdateUserError>> HandleAsync(
        Command cmd, IUsersRepository repo, IClock clock, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.FullName))
            return Result<GetUser.Response, UpdateUserError>.Failure(
                new UpdateUserError.ValidationFailed("FullName requerido"));

        var user = await repo.GetByIdAsync(cmd.Id, ct);
        if (user is null || user.DeletedAt is not null)
            return Result<GetUser.Response, UpdateUserError>.Failure(
                new UpdateUserError.NotFound());

        user.UpdateProfile(cmd.FullName, cmd.DocumentType, cmd.DocumentNumber, cmd.Phone, clock.UtcNow);
        await repo.UpdateAsync(user, ct);

        return Result<GetUser.Response, UpdateUserError>.Success(GetUser.Map(user));
    }
}

// =============================================================================
// ChangeUserStatus — cambiar status (ACTIVE/INACTIVE/BLOCKED)
// Patron compensacion: BD update primero, luego AdminDisable/Enable en Cognito.
// =============================================================================
public static class ChangeUserStatus
{
    public sealed record Command(Guid Id, UserStatus NewStatus, Guid? ChangedByUserId);

    public abstract record ChangeStatusError(string Code, string Message)
    {
        public sealed record NotFound() : ChangeStatusError("USER_NOT_FOUND", "Usuario no encontrado");
        public sealed record InvalidTransition(string Detail) : ChangeStatusError("INVALID_TRANSITION", Detail);
        public sealed record CognitoSyncFail(string Detail) : ChangeStatusError("COGNITO_SYNC_FAIL", $"Cognito fallo: {Detail}");
    }

    public static async Task<Result<GetUser.Response, ChangeStatusError>> HandleAsync(
        Command cmd,
        IUsersRepository repo,
        ICognitoDirectory cognito,
        IUnitOfWork uow,
        IClock clock,
        CancellationToken ct = default)
    {
        var user = await repo.GetByIdAsync(cmd.Id, ct);
        if (user is null || user.DeletedAt is not null)
            return Result<GetUser.Response, ChangeStatusError>.Failure(
                new ChangeStatusError.NotFound());

        if (cmd.NewStatus == UserStatus.DELETED)
            return Result<GetUser.Response, ChangeStatusError>.Failure(
                new ChangeStatusError.InvalidTransition("Usa DeleteUser para eliminar"));

        await using var tx = await uow.BeginTransactionAsync(ct);
        try
        {
            user.ChangeStatus(cmd.NewStatus, clock.UtcNow);
            await repo.UpdateAsync(user, ct);
            await uow.SaveChangesAsync(ct);

            // Sync con Cognito: disable cuando va a INACTIVE/BLOCKED, enable cuando vuelve a ACTIVE.
            if (user.CognitoSub is not null)
            {
                switch (cmd.NewStatus)
                {
                    case UserStatus.INACTIVE:
                    case UserStatus.BLOCKED:
                        await cognito.AdminDisableUserAsync(user.Email, ct);
                        await cognito.AdminUserGlobalSignOutAsync(user.Email, ct);
                        break;
                    case UserStatus.ACTIVE:
                        await cognito.AdminEnableUserAsync(user.Email, ct);
                        break;
                }
            }

            await tx.CommitAsync(ct);
            return Result<GetUser.Response, ChangeStatusError>.Success(GetUser.Map(user));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Result<GetUser.Response, ChangeStatusError>.Failure(
                new ChangeStatusError.CognitoSyncFail(ex.Message));
        }
    }
}

// =============================================================================
// DeleteUser — soft delete + Cognito disable + global signout
// =============================================================================
public static class DeleteUser
{
    public sealed record Command(Guid Id, Guid? DeletedByUserId);

    public abstract record DeleteUserError(string Code, string Message)
    {
        public sealed record NotFound() : DeleteUserError("USER_NOT_FOUND", "Usuario no encontrado");
        public sealed record CognitoSyncFail(string Detail) : DeleteUserError("COGNITO_SYNC_FAIL", $"Cognito fallo: {Detail}");
    }

    public static async Task<Result<Unit, DeleteUserError>> HandleAsync(
        Command cmd,
        IUsersRepository repo,
        ICognitoDirectory cognito,
        IUnitOfWork uow,
        IClock clock,
        CancellationToken ct = default)
    {
        var user = await repo.GetByIdAsync(cmd.Id, ct);
        if (user is null || user.DeletedAt is not null)
            return Result<Unit, DeleteUserError>.Failure(new DeleteUserError.NotFound());

        await using var tx = await uow.BeginTransactionAsync(ct);
        try
        {
            user.Delete(clock.UtcNow);
            await repo.UpdateAsync(user, ct);
            await uow.SaveChangesAsync(ct);

            if (user.CognitoSub is not null)
            {
                await cognito.AdminDisableUserAsync(user.Email, ct);
                await cognito.AdminUserGlobalSignOutAsync(user.Email, ct);
            }

            await tx.CommitAsync(ct);
            return Result<Unit, DeleteUserError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Result<Unit, DeleteUserError>.Failure(
                new DeleteUserError.CognitoSyncFail(ex.Message));
        }
    }
}

/// <summary>Token para use cases que no devuelven valor (estilo Unit / Void).</summary>
public sealed record Unit
{
    public static readonly Unit Value = new();
    private Unit() { }
}
