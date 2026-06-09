namespace Flit.Modules.Companies.Domain;

/// <summary>Claves de módulo permitidas en company_module_configs (CHECK ddl/30).</summary>
public static class CompanyModuleKey
{
    public const string Registration = "registration";
    public const string Transfers = "transfers";
    public const string Company = "company";
    public const string RuntContingency = "runt_contingency";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Registration, Transfers, Company, RuntContingency,
    };
}
