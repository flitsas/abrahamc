using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Lectura de identity.global_auth_settings (singleton global).</summary>
public interface IGlobalAuthSettingsReader
{
    Task<GlobalAuthSettingsRow> GetAsync(CancellationToken ct = default);
}
