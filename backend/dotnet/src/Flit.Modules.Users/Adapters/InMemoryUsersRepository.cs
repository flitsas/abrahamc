using System.Collections.Concurrent;
using Flit.Modules.Users.Application;
using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;

namespace Flit.Modules.Users.Adapters;

/// <summary>
/// Adapter InMemory para desarrollo local sin Postgres.
/// La implementacion EF Core vive en Flit.Infrastructure.Repositories.EfUsersRepository
/// (pendiente Chunk 4 — backlog).
///
/// Soft delete: los usuarios con DeletedAt != null se filtran automaticamente
/// excepto en GetByIdAsync (que devuelve el usuario aunque este eliminado).
/// </summary>
public sealed class InMemoryUsersRepository : IUsersRepository
{
    private readonly ConcurrentDictionary<Guid, User> _byId = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(id, out var user) ? user : null);

    public Task<User?> GetByCognitoSubAsync(string cognitoSub, CancellationToken ct = default)
    {
        var user = _byId.Values.FirstOrDefault(u =>
            string.Equals(u.CognitoSub, cognitoSub, StringComparison.Ordinal)
            && u.DeletedAt is null);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = _byId.Values.FirstOrDefault(u =>
            string.Equals(u.Email, normalized, StringComparison.Ordinal)
            && u.DeletedAt is null);
        return Task.FromResult(user);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(_byId.Values.Any(u =>
            string.Equals(u.Email, normalized, StringComparison.Ordinal)
            && u.DeletedAt is null));
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> ListAsync(
        int page, int limit, string? search, CancellationToken ct = default)
    {
        IEnumerable<User> q = _byId.Values.Where(u => u.DeletedAt is null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u =>
                u.FullName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (u.DocumentNumber != null && u.DocumentNumber.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        IReadOnlyList<User> result =
            [.. q.OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)];
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        IEnumerable<User> q = _byId.Values.Where(u => u.DeletedAt is null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u =>
                u.FullName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(q.Count());
    }
}

/// <summary>
/// Stub para uso en dev local: simula AdminCreateUser/AdminInitiateAuth de Cognito.
/// Generates un sub fake (UUIDv7) y simula respuesta exitosa.
/// La implementacion real (HybridCognitoDirectoryAdapter) vive en
/// Flit.Modules.Identity.Adapters (pendiente Fase 7).
/// </summary>
public sealed class StubCognitoDirectory : ICognitoDirectory
{
    /// <summary>
    /// Sub determinístico para que la BD pueda referenciarlo: "stub-{emailLower}".
    /// Permite que un seed local de usuario tenga un cognito_sub conocido.
    /// </summary>
    public static string SubForEmail(string email) =>
        $"stub-{email.Trim().ToLowerInvariant()}";

    public Task<CognitoUserResult> AdminCreateUserAsync(
        string email, string tempPassword, Guid appUserId, CancellationToken ct = default)
        => Task.FromResult(new CognitoUserResult(
            Sub: SubForEmail(email),
            Username: email));

    public Task AdminSetUserPasswordAsync(
        string username, string newPassword, bool permanent, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<CognitoTokens> AdminInitiateAuthAsync(
        string username, string password, CancellationToken ct = default)
        => Task.FromResult(BuildTokens(username));

    public Task<CognitoTokens> AdminRefreshAuthAsync(string refreshToken, CancellationToken ct = default)
    {
        // El refresh token (stub) lleva el email en el payload tipo "rt-{email}".
        var email = refreshToken.StartsWith("rt-", StringComparison.Ordinal)
            ? refreshToken[3..]
            : "unknown@stub";
        return Task.FromResult(BuildTokens(email, refreshToken));
    }

    public Task AdminDisableUserAsync(string username, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AdminEnableUserAsync(string username, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AdminUserGlobalSignOutAsync(string username, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Construye tokens stub con un IdToken con forma JWT (header.payload.signature)
    /// donde el payload contiene `sub` derivado del email. Login.ExtractSubFromJwt
    /// decodifica el segundo segmento (sin validar firma — solo extraccion local).
    /// </summary>
    private static CognitoTokens BuildTokens(string email, string? refreshToken = null)
    {
        var sub = SubForEmail(email);
        var header = ToBase64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        // JSON manual para evitar dependencia de JsonSerializer (AOT-friendly).
        // El email se escapa minimo: comillas + backslash (caracteres validos en email no incluyen otros).
        var safeEmail = email.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var safeSub = sub.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var payloadJson = $"{{\"sub\":\"{safeSub}\",\"email\":\"{safeEmail}\"}}";
        var payload = ToBase64Url(payloadJson);
        var idToken = $"{header}.{payload}.stub-signature";

        return new CognitoTokens(
            AccessToken: $"stub-access-{Guid.CreateVersion7()}",
            IdToken: idToken,
            RefreshToken: refreshToken ?? $"rt-{email.Trim().ToLowerInvariant()}",
            ExpiresIn: 3600);
    }

    private static string ToBase64Url(string raw) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Generador stub para passwords temporales que cumplen la policy default de Cognito.
/// </summary>
public sealed class TempPasswordGenerator : IPasswordGenerator
{
    public string Generate()
    {
        // Policy default Cognito: 8+ chars, upper, lower, digit, special.
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string specials = "!@#$%^&*";

        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var buffer = new byte[12];
        rng.GetBytes(buffer);

        var pwd = new char[12];
        pwd[0] = lower[buffer[0] % lower.Length];
        pwd[1] = upper[buffer[1] % upper.Length];
        pwd[2] = digits[buffer[2] % digits.Length];
        pwd[3] = specials[buffer[3] % specials.Length];

        const string all = lower + upper + digits + specials;
        for (int i = 4; i < 12; i++)
            pwd[i] = all[buffer[i] % all.Length];

        // Shuffle
        var random = new Random(BitConverter.ToInt32(buffer, 0));
        return new string([.. pwd.OrderBy(_ => random.Next())]);
    }
}

/// <summary>
/// UnitOfWork InMemory: no-op para dev. La impl EF real (EfUnitOfWork)
/// envuelve DbContext en una transaccion.
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => Task.FromResult<IUnitOfWorkTransaction>(new NoopTransaction());

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

    private sealed class NoopTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
