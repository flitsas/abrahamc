using System.Data;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlOtConsolidatedDocOrderRepository(FlitDbContext db) : IOtConsolidatedDocOrderRepository
{
    public async Task<(OtConsolidatedDocOrderRecord Order, IReadOnlyList<OtConsolidatedDocOrderItemRecord> Items)?>
        GetActiveOrderWithItemsAsync(Guid trafficAgencyId, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await SetAgencyContextAsync(conn, trafficAgencyId, ct);

        Guid? orderId = null;
        OtConsolidatedDocOrderRecord? order = null;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, traffic_agency_id, version, is_active
                FROM ot.ot_consolidated_doc_orders
                WHERE traffic_agency_id = @agency_id
                  AND is_active = true
                  AND deleted_at IS NULL
                LIMIT 1
                """;
            cmd.Parameters.Add(new NpgsqlParameter("agency_id", NpgsqlDbType.Uuid) { Value = trafficAgencyId });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            orderId = reader.GetGuid(0);
            order = new OtConsolidatedDocOrderRecord(
                orderId.Value,
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetBoolean(3));
        }

        var items = new List<OtConsolidatedDocOrderItemRecord>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, order_id, traffic_agency_id, document_type_id, custom_label, position, source
                FROM ot.ot_consolidated_doc_order_items
                WHERE order_id = @order_id
                ORDER BY position ASC
                """;
            cmd.Parameters.Add(new NpgsqlParameter("order_id", NpgsqlDbType.Uuid) { Value = orderId!.Value });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new OtConsolidatedDocOrderItemRecord(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetInt32(5),
                    reader.GetString(6)));
            }
        }

        return (order!, items);
    }

    private static async Task SetAgencyContextAsync(
        NpgsqlConnection conn,
        Guid trafficAgencyId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_agency_id', @aid, true)";
        cmd.Parameters.Add(new NpgsqlParameter("aid", NpgsqlDbType.Text) { Value = trafficAgencyId.ToString() });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
