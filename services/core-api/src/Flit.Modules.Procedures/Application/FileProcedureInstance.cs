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
        var referenceContext = await repository.ResolveReferenceContextAsync(
            command.TenantId,
            command.TrafficAgencyId,
            command.ProcedureTypeCode,
            ct);
        var sequence = await repository.GetNextSequenceAsync(
            command.TenantId,
            command.ProcedureTypeId,
            command.TrafficAgencyId,
            ct);
        var referenceNumber = GenerateReferenceNumber(
            referenceContext.TypeCode,
            referenceContext.TenantCode,
            referenceContext.OtCode,
            sequence);

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
    /// Genera número compuesto <c>{TYPE}-{TENANT}_{OT}-{SEQ}</c> (#10079).
    /// </summary>
    public static string GenerateReferenceNumber(
        string typeCode,
        string tenantCode,
        string otCode,
        int sequence) =>
        ProcedureReferenceFormatter.GenerateReferenceNumber(typeCode, tenantCode, otCode, sequence);
}
