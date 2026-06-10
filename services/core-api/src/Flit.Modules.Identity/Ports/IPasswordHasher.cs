namespace Flit.Modules.Identity.Ports;

/// <summary>
/// Port para hashing de password (ADR-0006 §"Politica de contrasenas").
/// Default: Argon2id (32 MB, 4 iter, 1 paralelo). OWASP 2023+.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
