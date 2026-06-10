using System.Text.RegularExpressions;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Domain;

/// <summary>
/// Value Object Email. Inmutable. Valida formato RFC 5322 simplificado.
/// </summary>
public sealed partial record Email
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email, IdentityError> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result<Email, IdentityError>.Failure(
                new IdentityError.EmailInvalido("email no puede ser vacio"));

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Length > 255)
            return Result<Email, IdentityError>.Failure(
                new IdentityError.EmailInvalido("email excede 255 caracteres"));

        if (!EmailRegex().IsMatch(normalized))
            return Result<Email, IdentityError>.Failure(
                new IdentityError.EmailInvalido($"formato invalido: {raw}"));

        return Result<Email, IdentityError>.Success(new Email(normalized));
    }

    public override string ToString() => Value;

    [GeneratedRegex(
        @"^[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
