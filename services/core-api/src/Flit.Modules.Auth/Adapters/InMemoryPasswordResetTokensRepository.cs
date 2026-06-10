using System.Collections.Concurrent;
using Flit.Modules.Auth.Application;
using Flit.Modules.Users.Domain;

namespace Flit.Modules.Auth.Adapters;

/// <summary>
/// Adapter InMemory de IPasswordResetTokensRepository para development/tests.
/// Produccion usa EfPasswordResetTokensRepository (tabla identity.password_reset_tokens).
/// </summary>
public sealed class InMemoryPasswordResetTokensRepository : IPasswordResetTokensRepository
{
    private readonly ConcurrentDictionary<string, PasswordResetToken> _byHash = new();

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_byHash.TryGetValue(tokenHash, out var t) ? t : null);

    public Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        _byHash[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        _byHash[token.TokenHash] = token;
        return Task.CompletedTask;
    }
}
