using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Implementacion EF Core de IUsuariosRepository.
/// Reemplaza InMemoryUsuariosRepository cuando ConnectionStrings:Core esta configurado.
/// </summary>
public sealed class EfUsuariosRepository(FlitDbContext db) : IUsuariosRepository
{
    public async Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        await db.Usuarios.FindAsync([id], ct);

    public async Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct)
    {
        var lower = email.Trim().ToLowerInvariant();
        return await db.Usuarios
            .FirstOrDefaultAsync(u => u.Email.Value == lower, ct);
    }

    public async Task<bool> ExisteEmailAsync(string email, CancellationToken ct)
    {
        var lower = email.Trim().ToLowerInvariant();
        return await db.Usuarios.AnyAsync(u => u.Email.Value == lower, ct);
    }

    public async Task<bool> ExisteDocumentoAsync(string tipo, string numero, CancellationToken ct) =>
        await db.Usuarios.AnyAsync(
            u => u.Documento.Tipo.ToString() == tipo && u.Documento.Numero == numero, ct);

    public async Task<Result<Guid, IdentityError>> GuardarAsync(
        Usuario usuario, CancellationToken ct)
    {
        var entry = db.Entry(usuario);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            var existing = await db.Usuarios.FindAsync([usuario.Id], ct);
            if (existing is null)
                db.Usuarios.Add(usuario);
            // Si ya existe, EF Core lo trackea via Find y detecta cambios en Save
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid, IdentityError>.Success(usuario.Id);
    }
}
