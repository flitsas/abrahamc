using Flit.Modules.Users.Domain;

namespace Flit.Modules.Users.Ports;

/// <summary>
/// Repositorio del agregado User. La implementacion EF Core vive en
/// Flit.Infrastructure/Repositories/EfUsersRepository.cs.
/// </summary>
public interface IUsersRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByCognitoSubAsync(string cognitoSub, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListAsync(int page, int limit, string? search, CancellationToken ct = default);
    Task<int> CountAsync(string? search, CancellationToken ct = default);
}
