namespace Flit.Modules.Procedures.Ports;

public sealed record ProcedureVehicleEntry(
    Guid Id,
    Guid TenantId,
    Guid ProcedureInstanceId,
    string VehicleSubkind,
    string? LicensePlate,
    string? Vin,
    Guid CreatedBy,
    Guid UpdatedBy);

public interface IProcedureVehicleRepository
{
    Task UpsertAsync(ProcedureVehicleEntry entry, CancellationToken ct = default);

    Task<ProcedureVehicleEntry?> GetByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
