using Flit.Modules.Procedures.Domain;

namespace Flit.Modules.Procedures.Application;

/// <summary>
/// HU TRA-02 #9434 — Radicación con snapshot de configuración.
/// Persiste una nueva instancia en <c>procedures.procedure_instances</c> con
/// <c>config_snapshot</c> inmutable capturado al momento de radicar (ADR-0010).
/// Estado inicial siempre <c>borrador</c>.
/// </summary>
public static class FileProcedureInstance
{
    public sealed record Command(
        Guid TenantId,
        Guid ProcedureTypeId,
        Guid? TrafficAgencyId,
        string ProcedureTypeCode,
        Guid FiledByUserId,
        string ConfigSnapshotJson,
        DateTimeOffset? OverrideNow = null);

    public static async Task<(ProcedureInstance? Instance, ProcedureInstanceError? Error)> HandleAsync(
        Command command,
        IProcedureInstanceRepository repository,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.ConfigSnapshotJson)
            || command.ConfigSnapshotJson == "{}"
            || command.ConfigSnapshotJson == "null")
        {
            return (null, new ProcedureInstanceError(
                ProcedureInstanceErrorKind.NotFound,
                "config_snapshot no puede estar vacío al radicar."));
        }

        var now = command.OverrideNow ?? DateTimeOffset.UtcNow;
        var referenceNumber = GenerateReferenceNumber(now);

        var instance = new ProcedureInstance(
            Id: Guid.NewGuid(),
            TenantId: command.TenantId,
            ProcedureTypeId: command.ProcedureTypeId,
            TrafficAgencyId: command.TrafficAgencyId,
            ReferenceNumber: referenceNumber,
            State: ProcedureStates.Borrador,
            ConfigSnapshot: command.ConfigSnapshotJson,
            ConfigSchemaVersion: 1,
            AssignedToUserId: null,
            RadicatedAt: now,
            TotalAmount: 0m,
            CurrencyCode: "COP",
            CreatedAt: now,
            CreatedBy: command.FiledByUserId,
            UpdatedAt: now,
            UpdatedBy: command.FiledByUserId,
            RowVersion: 1);

        await repository.SaveAsync(instance, ct);
        return (instance, null);
    }

    /// <summary>
    /// Genera un número de radicación único con formato <c>TRA-YYYYMMDD-XXXXXX</c>
    /// (6 dígitos hex en mayúsculas desde un GUID nuevo). La unicidad final es garantizada
    /// por el constraint <c>uq_procedure_instances_reference_number</c> en la BD.
    /// </summary>
    public static string GenerateReferenceNumber(DateTimeOffset now)
    {
        var hex = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"TRA-{now:yyyyMMdd}-{hex}";
    }
}
