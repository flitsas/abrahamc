using System.Reflection;

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// Carga scripts SQL embebidos para migraciones híbridas (DDL convenciones FLIT + EF Core).
/// </summary>
internal static class MigrationSql
{
    private const string Prefix = "Flit.Infrastructure.Migrations.Sql.";

    public static string Load(string relativePath)
    {
        var resourceName = Prefix + relativePath.Replace('/', '.').Replace('\\', '.');
        var assembly = typeof(MigrationSql).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"SQL embebido no encontrado: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
