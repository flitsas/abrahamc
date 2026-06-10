using Flit.Modules.Users.Application;
using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// REST endpoints para Users (ADR-0010).
/// Permisos pendientes Fase 7 (HybridCognito): TODOS los endpoints requeriran
/// [RequirePermission("USERS.LIST")], [RequirePermission("USERS.CREATE")], etc.
/// Por ahora son publicos para development.
/// </summary>
public static class UsersEndpoints
{
    // ─── DTOs ───────────────────────────────────────────────────────
    public sealed record CreateUserRequest(
        string Email,
        string FullName,
        string? DocumentType,
        string? DocumentNumber,
        string? Phone);

    public sealed record UpdateUserRequest(
        string FullName,
        string? DocumentType,
        string? DocumentNumber,
        string? Phone);

    public sealed record ChangeStatusRequest(UserStatus Status);

    public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        // ─── GET /api/v1/users — list paginado ─────────────────────────
        group.MapGet("/", async (
            int? page, int? limit, string? search,
            IUsersRepository repo, CancellationToken ct) =>
        {
            var response = await ListUsers.HandleAsync(
                new ListUsers.Query(page ?? 1, limit ?? 20, search), repo, ct);
            return Results.Ok(response);
        })
        .WithName("ListUsers");

        // ─── GET /api/v1/users/{id} ────────────────────────────────────
        group.MapGet("/{id:guid}", async (
            Guid id, IUsersRepository repo, CancellationToken ct) =>
        {
            var user = await GetUser.HandleAsync(new GetUser.Query(id), repo, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        })
        .WithName("GetUser");

        // ─── POST /api/v1/users — crear usuario (sync con Cognito) ─────
        group.MapPost("/", async (
            CreateUserRequest req,
            IUsersRepository repo, ICognitoDirectory cognito,
            IUnitOfWork uow, IClock clock, IPasswordGenerator pwgen,
            CancellationToken ct) =>
        {
            var cmd = new CreateUser.Command(
                Email: req.Email,
                FullName: req.FullName,
                DocumentType: req.DocumentType,
                DocumentNumber: req.DocumentNumber,
                Phone: req.Phone,
                CreatedByUserId: null);

            var result = await CreateUser.HandleAsync(cmd, repo, cognito, uow, clock, pwgen, ct);
            return result.Match(
                ok => Results.Created($"/api/v1/users/{ok.Id}", ok),
                err => MapCreateUserError(err));
        })
        .WithName("CreateUser");

        // ─── PATCH /api/v1/users/{id} — update profile ─────────────────
        group.MapPatch("/{id:guid}", async (
            Guid id, UpdateUserRequest req,
            IUsersRepository repo, IClock clock, CancellationToken ct) =>
        {
            var cmd = new UpdateUser.Command(id, req.FullName, req.DocumentType, req.DocumentNumber, req.Phone);
            var result = await UpdateUser.HandleAsync(cmd, repo, clock, ct);
            return result.Match(
                ok => Results.Ok(ok),
                err => MapUpdateUserError(err));
        })
        .WithName("UpdateUser");

        // ─── PATCH /api/v1/users/{id}/status — cambiar status ──────────
        group.MapPatch("/{id:guid}/status", async (
            Guid id, ChangeStatusRequest req,
            IUsersRepository repo, ICognitoDirectory cognito,
            IUnitOfWork uow, IClock clock, CancellationToken ct) =>
        {
            var cmd = new ChangeUserStatus.Command(id, req.Status, ChangedByUserId: null);
            var result = await ChangeUserStatus.HandleAsync(cmd, repo, cognito, uow, clock, ct);
            return result.Match(
                ok => Results.Ok(ok),
                err => MapChangeStatusError(err));
        })
        .WithName("ChangeUserStatus");

        // ─── DELETE /api/v1/users/{id} — soft delete ───────────────────
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IUsersRepository repo, ICognitoDirectory cognito,
            IUnitOfWork uow, IClock clock, CancellationToken ct) =>
        {
            var cmd = new DeleteUser.Command(id, DeletedByUserId: null);
            var result = await DeleteUser.HandleAsync(cmd, repo, cognito, uow, clock, ct);
            return result.Match(
                _ => Results.NoContent(),
                err => MapDeleteUserError(err));
        })
        .WithName("DeleteUser");
    }

    // ─── Error mappers ─────────────────────────────────────────────────
    private static IResult MapCreateUserError(CreateUser.CreateUserError err) => err switch
    {
        CreateUser.CreateUserError.EmailDuplicated =>
            Results.Conflict(new { error = err.Code, message = err.Message }),
        CreateUser.CreateUserError.CognitoSyncFail =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 502),
        CreateUser.CreateUserError.ValidationFailed =>
            Results.BadRequest(new { error = err.Code, message = err.Message }),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapUpdateUserError(UpdateUser.UpdateUserError err) => err switch
    {
        UpdateUser.UpdateUserError.NotFound =>
            Results.NotFound(new { error = err.Code, message = err.Message }),
        UpdateUser.UpdateUserError.ValidationFailed =>
            Results.BadRequest(new { error = err.Code, message = err.Message }),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapChangeStatusError(ChangeUserStatus.ChangeStatusError err) => err switch
    {
        ChangeUserStatus.ChangeStatusError.NotFound =>
            Results.NotFound(new { error = err.Code, message = err.Message }),
        ChangeUserStatus.ChangeStatusError.InvalidTransition =>
            Results.BadRequest(new { error = err.Code, message = err.Message }),
        ChangeUserStatus.ChangeStatusError.CognitoSyncFail =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 502),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };

    private static IResult MapDeleteUserError(DeleteUser.DeleteUserError err) => err switch
    {
        DeleteUser.DeleteUserError.NotFound =>
            Results.NotFound(new { error = err.Code, message = err.Message }),
        DeleteUser.DeleteUserError.CognitoSyncFail =>
            Results.Json(new { error = err.Code, message = err.Message }, statusCode: 502),
        _ => Results.Problem(detail: err.Message, statusCode: 500, title: err.Code),
    };
}
