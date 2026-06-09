using System.Text.RegularExpressions;
using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Application;

/// <summary>Validación de contraseña contra identity.password_policies (HU #9418, RF-4.2).</summary>
public static class TramitesPasswordPolicyValidator
{
    public const string PolicyViolationCode = "PASSWORD_POLICY_VIOLATION";

    private static readonly Regex UpperRegex = new("[A-Z]", RegexOptions.Compiled);
    private static readonly Regex LowerRegex = new("[a-z]", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new("[0-9]", RegexOptions.Compiled);
    private static readonly Regex SymbolRegex = new(@"[^A-Za-z0-9]", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(string password, PasswordComplexityPolicyRow policy)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password) || password.Length < policy.MinLength)
            errors.Add($"La contraseña debe tener al menos {policy.MinLength} caracteres.");

        if (policy.RequireUppercase && !UpperRegex.IsMatch(password))
            errors.Add("Debe incluir al menos una letra mayúscula.");

        if (policy.RequireLowercase && !LowerRegex.IsMatch(password))
            errors.Add("Debe incluir al menos una letra minúscula.");

        if (policy.RequireDigit && !DigitRegex.IsMatch(password))
            errors.Add("Debe incluir al menos un dígito.");

        if (policy.RequireSymbol && !SymbolRegex.IsMatch(password))
            errors.Add("Debe incluir al menos un símbolo.");

        return errors;
    }
}
