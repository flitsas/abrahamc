using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// Provider local: credenciales en identity_credential (Argon2id).
/// MVP default. Single source of truth para usuarios sin Cognito.
/// </summary>
public sealed class LocalIdentityProvider : IIdentityProvider
{
    private readonly IUsuariosRepository _usuarios;
    private readonly ICredentialsRepository _creds;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public string Name => "Local";

    public LocalIdentityProvider(
        IUsuariosRepository usuarios,
        ICredentialsRepository creds,
        IPasswordHasher hasher,
        IClock clock)
    {
        _usuarios = usuarios;
        _creds = creds;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<Result<Unit, IdentityError>> VerifyCredentialsAsync(
        string email, string password, CancellationToken ct)
    {
        var usuario = await _usuarios.ObtenerPorEmailAsync(email, ct);
        if (usuario is null)
            return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());

        var hash = await _creds.ObtenerPasswordHashAsync(usuario.Id, ct);
        if (hash is null || !_hasher.Verify(password, hash))
            return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());

        return Result<Unit, IdentityError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, IdentityError>> CreateCredentialAsync(
        Guid userId, string email, string password, CancellationToken ct)
    {
        var hash = _hasher.Hash(password);
        await _creds.GuardarPasswordHashAsync(userId, hash, _clock.UtcNow, ct);
        return Result<Unit, IdentityError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, IdentityError>> DeleteCredentialAsync(
        Guid userId, string email, CancellationToken ct)
    {
        await _creds.EliminarAsync(userId, ct);
        return Result<Unit, IdentityError>.Success(Unit.Value);
    }

    public Task SignOutAsync(string email, CancellationToken ct)
    {
        // No-op: la revocacion la hace InMemoryRefreshTokenStore.RevokeAsync
        // sobre el jti del refresh actual. El access expira solo (15min).
        return Task.CompletedTask;
    }
}
