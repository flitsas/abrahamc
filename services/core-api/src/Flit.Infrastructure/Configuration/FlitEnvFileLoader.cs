namespace Flit.Infrastructure.Configuration;

/// <summary>
/// Carga <c>env.local</c> (desarrollo en máquina) o, si no existe, <c>env</c> en la raíz del monorepo
/// hacia variables de entorno antes de que ASP.NET Core construya
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// No sobrescribe variables ya definidas (Docker, CI, shell).
/// </summary>
public static class FlitEnvFileLoader
{
    private static readonly Dictionary<string, string> ConfigAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CONNECTION_STRING_CORE"] = "ConnectionStrings__Core",
            ["CORS_ORIGIN"] = "Cors__AllowedOrigins",
            ["FRONTEND_BASE_URL"] = "Identity__Onboarding__ActivationBaseUrl",
            ["SMTP_HOST"] = "Smtp__Host",
            ["SMTP_PORT"] = "Smtp__Port",
            ["SMTP_USER"] = "Smtp__User",
            ["SMTP_PASSWORD"] = "Smtp__Password",
            ["SMTP_FROM"] = "Smtp__From",
            ["SMTP_FROM_NAME"] = "Smtp__FromName",
            ["VERIFIK_API_TOKEN"] = "Verifik__ApiToken",
            ["VERIFIK_BASE_URL"] = "Verifik__BaseUrl",
            ["VERIFIK_AUTH_SCHEME"] = "Verifik__AuthScheme",
            ["VERIFIK_TIMEOUT_SECONDS"] = "Verifik__TimeoutSeconds",
        };

    public static bool LoadIfPresent(string? explicitPath = null)
    {
        var path = explicitPath ?? FindEnvFile();
        if (path is null)
            return false;

        foreach (var (key, value) in ParseFile(path))
        {
            SetIfUnset(key, value);

            if (ConfigAliases.TryGetValue(key, out var alias))
                SetIfUnset(alias, value);
        }

        return true;
    }

    internal static string? FindEnvFile()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var packageJson = Path.Combine(dir.FullName, "package.json");
            if (!File.Exists(packageJson))
            {
                dir = dir.Parent;
                continue;
            }

            var envLocalPath = Path.Combine(dir.FullName, "env.local");
            if (File.Exists(envLocalPath))
                return envLocalPath;

            var envPath = Path.Combine(dir.FullName, "env");
            if (File.Exists(envPath))
                return envPath;

            return null;
        }

        return null;
    }

    internal static IEnumerable<KeyValuePair<string, string>> ParseFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var parsed = TryParseLine(rawLine);
            if (parsed is not null)
                yield return parsed.Value;
        }
    }

    internal static KeyValuePair<string, string>? TryParseLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            return null;

        var separator = line.IndexOf('=');
        if (separator <= 0)
            return null;

        var key = line[..separator].Trim();
        var value = Unquote(line[(separator + 1)..].Trim());

        if (key.Length == 0)
            return null;

        return new KeyValuePair<string, string>(key, value);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                return value[1..^1];
            }
        }

        return value;
    }

    private static void SetIfUnset(string key, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}
