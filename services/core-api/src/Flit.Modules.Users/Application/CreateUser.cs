using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Users.Application;

/// <summary>
/// Caso de uso: crear usuario en BD local + Cognito con compensacion (ADR-0010).
///
/// 1. Validar email no duplicado en BD
/// 2. INSERT users con cognito_sub NULL (en transaccion)
/// 3. AdminCreateUser en Cognito (timeout 5s, MessageAction=SUPPRESS)
/// 4. UPDATE users SET cognito_sub
/// 5. COMMIT
/// Si Cognito falla en paso 3, ROLLBACK y retornar 502.
/// </summary>
public static class CreateUser
{
    public sealed record Command(
        string Email,
        string FullName,
        string? DocumentType,
        string? DocumentNumber,
        string? Phone,
        Guid? CreatedByUserId);

    public sealed record Response(Guid Id, string Email, string TempPassword);

    public abstract record CreateUserError(string Code, string Message)
    {
        public sealed record EmailDuplicated()
            : CreateUserError("EMAIL_DUPLICATED", "Ya existe un usuario activo con ese email");

        public sealed record CognitoSyncFail(string Detail)
            : CreateUserError("COGNITO_SYNC_FAIL", $"Cognito no completo la operacion: {Detail}");

        public sealed record ValidationFailed(string Detail)
            : CreateUserError("VALIDATION_FAILED", Detail);
    }

    public static async Task<Result<Response, CreateUserError>> HandleAsync(
        Command cmd,
        IUsersRepository repo,
        ICognitoDirectory cognito,
        IUnitOfWork uow,
        IClock clock,
        IPasswordGenerator passwordGenerator,
        CancellationToken ct = default)
    {
        // 1. Validacion basica
        if (string.IsNullOrWhiteSpace(cmd.Email))
            return Result<Response, CreateUserError>.Failure(
                new CreateUserError.ValidationFailed("Email requerido"));
        if (string.IsNullOrWhiteSpace(cmd.FullName))
            return Result<Response, CreateUserError>.Failure(
                new CreateUserError.ValidationFailed("FullName requerido"));

        // 2. Duplicado
        if (await repo.EmailExistsAsync(cmd.Email, ct))
            return Result<Response, CreateUserError>.Failure(
                new CreateUserError.EmailDuplicated());

        // 3. Begin transaction
        await using var tx = await uow.BeginTransactionAsync(ct);
        try
        {
            var now = clock.UtcNow;
            var user = User.Create(
                email: cmd.Email,
                fullName: cmd.FullName,
                documentType: cmd.DocumentType,
                documentNumber: cmd.DocumentNumber,
                phone: cmd.Phone,
                createdByUserId: cmd.CreatedByUserId,
                now: now);

            await repo.AddAsync(user, ct);
            await uow.SaveChangesAsync(ct);

            // 4. AdminCreateUser en Cognito
            var tempPassword = passwordGenerator.Generate();
            var cognitoResult = await cognito.AdminCreateUserAsync(
                email: user.Email,
                tempPassword: tempPassword,
                appUserId: user.Id,
                ct: ct);

            // 5. Asociar sub y commit
            user.LinkCognitoSub(cognitoResult.Sub, clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<Response, CreateUserError>.Success(
                new Response(user.Id, user.Email, tempPassword));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Result<Response, CreateUserError>.Failure(
                new CreateUserError.CognitoSyncFail(ex.Message));
        }
    }
}

/// <summary>Puerto para generar contraseñas temporales que cumplan policy Cognito.</summary>
public interface IPasswordGenerator
{
    string Generate();
}

/// <summary>Puerto minimo para coordinar transacciones desde casos de uso.</summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
