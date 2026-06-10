namespace Flit.Modules.Companies.Ports;

/// <summary>Ambient context para RLS en la conexión PostgreSQL del request actual.</summary>
public static class CompaniesSessionAmbient
{
    private static readonly AsyncLocal<ICompaniesSessionContext?> Current = new();

    public static void Set(ICompaniesSessionContext? context) => Current.Value = context;

    public static ICompaniesSessionContext? Get() => Current.Value;
}
