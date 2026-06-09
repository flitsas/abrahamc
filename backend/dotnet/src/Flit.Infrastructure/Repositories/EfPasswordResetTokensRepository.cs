using Flit.Infrastructure.Persistence;
using Flit.Modules.Auth.Application;
using Flit.Modules.Users.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF Core de PasswordResetToken (tabla identity.password_reset_tokens).
/// Scoped (DbContext es Scoped).
/// </summary>
public sealed class EfPasswordResetTokensRepository : IPasswordResetTokensRepository
{
    private readonly FlitDbContext _db;

    public EfPasswordResetTokensRepository(FlitDbContext db) => _db = db;

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        await _db.PasswordResetTokens.AddAsync(token, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        _db.PasswordResetTokens.Update(token);
        return _db.SaveChangesAsync(ct);
    }
}
