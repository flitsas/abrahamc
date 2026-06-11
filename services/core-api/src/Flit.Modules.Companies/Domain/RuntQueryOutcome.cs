namespace Flit.Modules.Companies.Domain;

/// <summary>Outcomes de intento de consulta vehicular (#9690).</summary>
public static class RuntQueryOutcome
{
    public const string Ok = "ok";
    public const string Success = "success";
    public const string NotFound = "not_found";
    public const string Failed = "failed";
    public const string Timeout = "timeout";
    public const string CircuitOpen = "circuit_open";
}
