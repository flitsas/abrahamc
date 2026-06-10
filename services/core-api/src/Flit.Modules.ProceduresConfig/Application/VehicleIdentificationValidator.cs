using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9427 — VIN si no matriculado; placa si matriculado.</summary>
public static class VehicleIdentificationValidator
{
    private static readonly System.Text.RegularExpressions.Regex VinPattern =
        new(@"^[A-HJ-NPR-Z0-9]{17}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex PlatePattern =
        new(@"^[A-Z]{3}[0-9]{3}$|^[A-Z]{3}[0-9]{2}[A-Z]$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool IsValidVin(string? vin) =>
        !string.IsNullOrWhiteSpace(vin) && VinPattern.IsMatch(vin.Trim().ToUpperInvariant());

    public static bool IsValidPlate(string? plate) =>
        !string.IsNullOrWhiteSpace(plate) && PlatePattern.IsMatch(plate.Trim().ToUpperInvariant());

    public static ProcedureActorError? ValidateVehicleFields(IReadOnlyDictionary<string, string?> fields)
    {
        var matriculado = ParseMatriculado(fields);
        if (matriculado is true)
        {
            if (!IsValidPlate(fields.GetValueOrDefault("placa")))
            {
                return ProcedureActorError.InvalidVehicle("Placa obligatoria y con formato válido cuando el vehículo está matriculado.");
            }
        }
        else
        {
            if (!IsValidVin(fields.GetValueOrDefault("vin")))
            {
                return ProcedureActorError.InvalidVehicle("VIN obligatorio y con 17 caracteres válidos cuando el vehículo no está matriculado.");
            }
        }

        return null;
    }

    private static bool? ParseMatriculado(IReadOnlyDictionary<string, string?> fields)
    {
        if (!fields.TryGetValue("matriculado", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "si" or "sí" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => null,
        };
    }
}
