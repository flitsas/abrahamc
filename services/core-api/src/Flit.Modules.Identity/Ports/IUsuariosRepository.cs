using Flit.Modules.Identity.Domain;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Ports;

/// <summary>Repositorio del aggregate Usuario (Hexagonal).</summary>
public interface IUsuariosRepository
{
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct);
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct);
    Task<bool> ExisteEmailAsync(string email, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(string tipo, string numero, CancellationToken ct);
    Task<Result<Guid, IdentityError>> GuardarAsync(Usuario usuario, CancellationToken ct);
}
