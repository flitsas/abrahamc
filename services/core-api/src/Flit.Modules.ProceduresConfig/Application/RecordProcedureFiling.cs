using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9429 AC2 — radicación con omisiones auditadas en snapshot.</summary>
public static class RecordProcedureFiling
{
    public sealed record Command(
        Guid TenantId,
        Guid FiledByUserId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        IReadOnlyList<string>? OmittedQueries,
        ResolvedProcedureConfiguration Config);

    public sealed record FilingRecord(
        Guid Id,
        Guid TenantId,
        string ProcedureTypeCode,
        string ConfigSnapshotJson) : ITenantScopedRow;

    private static readonly List<FilingRecord> Store = [];

    /// <summary>Solo para tests: vacía el store estático entre ejecuciones.</summary>
    public static void ResetStoreForTesting() => Store.Clear();

    public static async Task<(FilingRecord? Ok, ProcedureActorError? Error)> HandleAsync(
        Command command,
        IProcedureFilingAuditPort auditPort,
        CancellationToken ct = default)
    {
        if (LeasingLocatarioRules.RejectsOmissionOfMandatorySimit(
                command.ProcedureTypeCode,
                command.EdgeCode,
                command.OmittedQueries ?? []))
        {
            return (null, new ProcedureActorError(
                ProcedureActorErrorKind.MandatoryQueryOmissionForbidden,
                "No se puede omitir SIMIT en trámite MAT_LEASING / arista locatario."));
        }

        var omitted = command.OmittedQueries ?? [];
        if (omitted.Count > 0)
        {
            await auditPort.RecordOmittedQueriesAsync(
                command.TenantId,
                command.FiledByUserId,
                command.ProcedureTypeCode,
                omitted,
                ct);
        }

        // ADR-0010: snapshot inmutable de toda la configuración resuelta al momento de radicar.
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            procedureTypeCode = command.Config.Code,
            procedureTypeId = command.Config.ProcedureTypeId,
            familyCode = command.Config.FamilyCode,
            maxSteps = command.Config.MaxSteps,
            resolutionLayers = command.Config.ResolutionLayers,
            edges = command.Config.Edges.Select(e => new
            {
                code = e.Code,
                name = e.Name,
                edgeKind = e.EdgeKind,
                isActive = e.IsActive,
                isRequired = e.IsRequired,
                displayOrder = e.DisplayOrder,
                roleLabel = e.RoleLabel,
            }),
            queries = command.Config.Queries.Select(q => new
            {
                connectorCode = q.ConnectorCode,
                edgeCode = q.EdgeCode,
                isMandatory = q.IsMandatory,
                isOmitible = q.IsOmitible,
                personKindFilter = q.PersonKindFilter,
            }),
            requiredDocuments = command.Config.RequiredDocuments.Select(d => new
            {
                documentTypeCode = d.DocumentTypeCode,
                edgeCode = d.EdgeCode,
                kind = d.Kind,
                isRequired = d.IsRequired,
                actorRole = d.ActorRole,
            }),
            omittedQueries = omitted.Select(o => new { connector = o, edge = command.EdgeCode, omittedByUserId = command.FiledByUserId }),
            filedByUserId = command.FiledByUserId,
            filedAt = DateTimeOffset.UtcNow,
        });

        var record = new FilingRecord(Guid.NewGuid(), command.TenantId, command.ProcedureTypeCode, snapshot);
        Store.Add(record);
        return (record, null);
    }

    public static IReadOnlyList<FilingRecord> ListFilingsForTenant(Guid tenantId) =>
        TenantIsolationFilter.Apply(tenantId, Store);
}
