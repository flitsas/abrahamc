using System.Text.Json;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

public enum VehicleTenantAccessErrorCode
{
    MissingPlate,
    VehicleNotOwned,
    OtNotAuthorized,
}

public sealed record VehicleTenantAccessError(
    VehicleTenantAccessErrorCode Code,
    string ErrorCode,
    string Message);

public static class EnforceVehicleTenantAccess
{
    public sealed record Command(Guid TenantId, Guid UserId, string Plate);

    public sealed record AllowResponse(
        bool Allowed,
        bool OnlyOwnVehicles,
        bool ExemptUser,
        bool ExternalPlate);

    public static async Task<Result<AllowResponse, VehicleTenantAccessError>> HandleAsync(
        Command cmd,
        ICompanyModuleConfigsRepository configRepo,
        ITenantGovernanceRepository governanceRepo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Plate))
        {
            return Result<AllowResponse, VehicleTenantAccessError>.Failure(
                new VehicleTenantAccessError(
                    VehicleTenantAccessErrorCode.MissingPlate,
                    "MISSING_PLATE",
                    "plate es requerida."));
        }

        var companyConfig = await configRepo.GetByTenantAndModuleAsync(
            cmd.TenantId,
            CompanyModuleKey.Company,
            ct);

        var onlyOwn = ParseOnlyOwnVehicles(companyConfig?.ConfigJson);
        if (!onlyOwn)
        {
            return Result<AllowResponse, VehicleTenantAccessError>.Success(
                new AllowResponse(true, false, false, IsExternalPlate(cmd.Plate)));
        }

        if (!IsExternalPlate(cmd.Plate))
        {
            return Result<AllowResponse, VehicleTenantAccessError>.Success(
                new AllowResponse(true, true, false, false));
        }

        var exempt = await governanceRepo.IsUserExemptFromOnlyOwnAsync(cmd.TenantId, cmd.UserId, ct);
        if (exempt)
        {
            return Result<AllowResponse, VehicleTenantAccessError>.Success(
                new AllowResponse(true, true, true, true));
        }

        return Result<AllowResponse, VehicleTenantAccessError>.Failure(
            new VehicleTenantAccessError(
                VehicleTenantAccessErrorCode.VehicleNotOwned,
                "VEHICLE_NOT_OWNED",
                "El vehículo no pertenece al tenant y el operador no tiene excepción."));
    }

    internal static bool ParseOnlyOwnVehicles(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.TryGetProperty("only_own_vehicles", out var prop)
                && prop.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Mock DEV: placas que empiezan por EXT son de terceros.</summary>
    internal static bool IsExternalPlate(string plate) =>
        plate.Trim().StartsWith("EXT", StringComparison.OrdinalIgnoreCase);
}

public static class AssertTrafficAgencyAuthorized
{
    public sealed record Command(Guid TenantId, Guid TrafficAgencyId);

    public sealed record AllowResponse(bool Authorized, bool MatrixEntryFound, bool IsEnabled);

    public static async Task<Result<AllowResponse, VehicleTenantAccessError>> HandleAsync(
        Command cmd,
        ITenantGovernanceRepository governanceRepo,
        CancellationToken ct = default)
    {
        var enabled = await governanceRepo.GetTrafficAgencyEnabledAsync(
            cmd.TenantId,
            cmd.TrafficAgencyId,
            ct);

        if (enabled is null)
        {
            return Result<AllowResponse, VehicleTenantAccessError>.Success(
                new AllowResponse(true, false, true));
        }

        if (enabled == false)
        {
            return Result<AllowResponse, VehicleTenantAccessError>.Failure(
                new VehicleTenantAccessError(
                    VehicleTenantAccessErrorCode.OtNotAuthorized,
                    "OT_NOT_AUTHORIZED",
                    "La OT no está autorizada para este tenant."));
        }

        return Result<AllowResponse, VehicleTenantAccessError>.Success(
            new AllowResponse(true, true, true));
    }
}
