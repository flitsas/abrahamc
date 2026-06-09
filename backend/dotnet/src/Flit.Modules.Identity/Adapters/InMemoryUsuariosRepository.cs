using System.Collections.Concurrent;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// Adapter in-memory thread-safe para tests + dev sin Postgres.
/// Producción: EfCoreUsuariosRepository (Fase 9 cutover).
/// </summary>
public sealed class InMemoryUsuariosRepository : IUsuariosRepository
{
    private readonly ConcurrentDictionary<Guid, Usuario> _store = new();

    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct)
    {
        _store.TryGetValue(id, out var u);
        return Task.FromResult(u);
    }

    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct)
    {
        var lower = email.Trim().ToLowerInvariant();
        var found = _store.Values.FirstOrDefault(u => u.Email.Value == lower);
        return Task.FromResult(found);
    }

    public Task<bool> ExisteEmailAsync(string email, CancellationToken ct)
    {
        var lower = email.Trim().ToLowerInvariant();
        return Task.FromResult(_store.Values.Any(u => u.Email.Value == lower));
    }

    public Task<bool> ExisteDocumentoAsync(string tipo, string numero, CancellationToken ct)
    {
        return Task.FromResult(
            _store.Values.Any(u =>
                u.Documento.Tipo.ToString() == tipo &&
                u.Documento.Numero == numero));
    }

    public Task<Result<Guid, IdentityError>> GuardarAsync(Usuario usuario, CancellationToken ct)
    {
        _store[usuario.Id] = usuario;
        return Task.FromResult(Result<Guid, IdentityError>.Success(usuario.Id));
    }
}
