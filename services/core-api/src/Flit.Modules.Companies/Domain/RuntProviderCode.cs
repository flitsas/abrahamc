namespace Flit.Modules.Companies.Domain;

/// <summary>Proveedores RUNT / contingencia (CHECK integrations.runt_sync_log).</summary>
public static class RuntProviderCode
{
    public const string Runt = "runt";
    public const string Verifik = "verifik";
    public const string Intempo = "intempo";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Runt, Verifik, Intempo,
    };
}
