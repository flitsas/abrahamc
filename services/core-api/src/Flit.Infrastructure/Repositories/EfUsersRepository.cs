using Flit.Infrastructure.Persistence;
using Flit.Modules.Users.Application;
using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF Core del agregado User (ADR-0010).
/// HasQueryFilter del UserConfiguration filtra DeletedAt IS NULL por defecto;
/// usar IgnoreQueryFilters() solo cuando se quiere ver soft-deleted.
/// </summary>
public sealed class EfUsersRepository : IUsersRepository
{
    private readonly FlitDbContext _db;

    public EfUsersRepository(FlitDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByCognitoSubAsync(string cognitoSub, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.CognitoSub == cognitoSub, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Users.AnyAsync(u => u.Email == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<User>> ListAsync(
        int page, int limit, string? search, CancellationToken ct = default)
    {
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(u =>
                EF.Functions.ILike(u.FullName, s) ||
                EF.Functions.ILike(u.Email, s) ||
                (u.DocumentNumber != null && EF.Functions.ILike(u.DocumentNumber, s)));
        }

        var list = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);
        return list;
    }

    public Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(u =>
                EF.Functions.ILike(u.FullName, s) ||
                EF.Functions.ILike(u.Email, s));
        }
        return q.CountAsync(ct);
    }
}

/// <summary>
/// UnitOfWork EF Core. Envuelve DbContext en una transaccion explicita
/// para que los casos de uso con compensacion (Cognito sync) puedan hacer
/// rollback atomico si la integracion externa falla.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly FlitDbContext _db;

    public EfUnitOfWork(FlitDbContext db) => _db = db;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new EfTransaction(tx);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    private sealed class EfTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _tx;
        public EfTransaction(IDbContextTransaction tx) => _tx = tx;
        public Task CommitAsync(CancellationToken ct = default) => _tx.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => _tx.RollbackAsync(ct);
        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
