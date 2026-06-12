using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlOtDocumentLabelRepository(FlitDbContext db) : IOtDocumentLabelRepository
{
    private sealed class LabelRow
    {
        public Guid id { get; init; }
        public Guid traffic_agency_id { get; init; }
        public string label_key { get; init; } = "";
        public string display_name { get; init; } = "";
        public bool exclude_from_bundle { get; init; }
    }

    public async Task<IReadOnlyList<OtDocumentLabelRecord>> ListByAgencyAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<LabelRow>($"""
            SELECT id, traffic_agency_id, label_key, display_name, exclude_from_bundle
            FROM ot.document_labels
            WHERE traffic_agency_id = {trafficAgencyId}
              AND deleted_at IS NULL
            ORDER BY display_name
            """).ToListAsync(ct);

        return rows.Select(r => new OtDocumentLabelRecord(
            r.id, r.traffic_agency_id, r.label_key, r.display_name, r.exclude_from_bundle)).ToList();
    }

    public async Task<OtDocumentLabelRecord> CreateAsync(
        Guid trafficAgencyId,
        string labelKey,
        string displayName,
        bool excludeFromBundle,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ot.document_labels (
              id, traffic_agency_id, label_key, display_name, exclude_from_bundle,
              created_by, updated_by)
            VALUES (
              {id}, {trafficAgencyId}, {labelKey}, {displayName}, {excludeFromBundle},
              {actorUserId}, {actorUserId})
            """, ct);

        return new OtDocumentLabelRecord(id, trafficAgencyId, labelKey, displayName, excludeFromBundle);
    }

    public async Task<bool> SoftDeleteAsync(
        Guid trafficAgencyId,
        Guid labelId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ot.document_labels
            SET deleted_at = now(), deleted_by = {actorUserId}, updated_by = {actorUserId}, updated_at = now()
            WHERE id = {labelId}
              AND traffic_agency_id = {trafficAgencyId}
              AND deleted_at IS NULL
            """, ct);

        return affected > 0;
    }
}
