using System.Text.RegularExpressions;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Domain;

/// <summary>Tipo de documento colombiano (ADR-0006, ADR-0007).</summary>
public enum TipoDocumento
{
    CC,
    CE,
    TI,
    PA,
    RC,
    NIT,
}

/// <summary>Value Object Documento (tipo + numero). Inmutable.</summary>
public sealed partial record Documento
{
    public TipoDocumento Tipo { get; }
    public string Numero { get; }

    private Documento(TipoDocumento tipo, string numero)
    {
        Tipo = tipo;
        Numero = numero;
    }

    public static Result<Documento, IdentityError> Create(TipoDocumento tipo, string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return Result<Documento, IdentityError>.Failure(
                new IdentityError.DocumentoInvalido("numero no puede ser vacio"));

        var clean = numero.Trim();

        if (!NumeroRegex().IsMatch(clean))
            return Result<Documento, IdentityError>.Failure(
                new IdentityError.DocumentoInvalido(
                    $"numero invalido (5-20 digitos): {numero}"));

        return Result<Documento, IdentityError>.Success(new Documento(tipo, clean));
    }

    public override string ToString() => $"{Tipo}:{Numero}";

    [GeneratedRegex(@"^\d{5,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroRegex();
}
