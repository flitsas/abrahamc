using System.Text.Json;
using System.Text.RegularExpressions;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>
/// Valida auth_config sin credenciales en claro (AC1 #9439).
/// Solo <c>secret_ref</c> con prefijo <c>vault://</c> cuando auth_type != none.
/// </summary>
public static partial class EndpointAuthConfigValidator
{
    private static readonly HashSet<string> DeniedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "api_key", "apikey", "apiKey", "token", "access_token",
        "accessToken", "secret", "client_secret", "clientSecret", "credential", "credentials",
        "bearer", "authorization", "private_key", "privateKey",
    };

    private static readonly Regex VaultRefPattern = VaultRefRegex();

    public static bool TryValidate(string authType, JsonElement authConfig, out string? error)
    {
        error = null;
        var normalizedAuth = string.IsNullOrWhiteSpace(authType) ? "none" : authType.Trim().ToLowerInvariant();

        if (normalizedAuth is not ("none" or "api_key" or "bearer" or "basic"))
        {
            error = "auth_type inválido.";
            return false;
        }

        if (normalizedAuth == "none")
        {
            if (authConfig.ValueKind == JsonValueKind.Object && authConfig.EnumerateObject().Any())
            {
                error = "auth_config debe ser vacío cuando auth_type es none.";
                return false;
            }

            return true;
        }

        if (authConfig.ValueKind != JsonValueKind.Object)
        {
            error = "auth_config debe ser un objeto JSON.";
            return false;
        }

        if (!authConfig.TryGetProperty("secret_ref", out var secretRefEl) ||
            secretRefEl.ValueKind != JsonValueKind.String)
        {
            error = "auth_config requiere secret_ref (referencia vault://).";
            return false;
        }

        var secretRef = secretRefEl.GetString();
        if (string.IsNullOrWhiteSpace(secretRef) || !VaultRefPattern.IsMatch(secretRef))
        {
            error = "secret_ref debe usar el formato vault://<clave>.";
            return false;
        }

        foreach (var prop in authConfig.EnumerateObject())
        {
            if (DeniedPropertyNames.Contains(prop.Name))
            {
                error = $"auth_config no permite la propiedad '{prop.Name}'.";
                return false;
            }

            if (prop.Name.Equals("secret_ref", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.String &&
                LooksLikePlaintextSecret(prop.Value.GetString()))
            {
                error = "auth_config no puede contener secretos en texto plano.";
                return false;
            }
        }

        return true;
    }

    public static JsonElement SanitizeForResponse(JsonElement authConfig)
    {
        if (authConfig.ValueKind != JsonValueKind.Object)
        {
            return JsonRuleElements.Parse("{}");
        }

        if (authConfig.TryGetProperty("secret_ref", out var secretRef) &&
            secretRef.ValueKind == JsonValueKind.String)
        {
            return JsonRuleElements.Parse(
                JsonSerializer.Serialize(new { secret_ref = secretRef.GetString() }));
        }

        return JsonRuleElements.Parse("{}");
    }

    private static bool LooksLikePlaintextSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("vault://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.Length >= 16 && !value.Contains(' ', StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^vault://[a-zA-Z0-9._\-/]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VaultRefRegex();
}
