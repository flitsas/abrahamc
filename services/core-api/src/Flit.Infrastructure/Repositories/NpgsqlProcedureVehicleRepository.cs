using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Procedures.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureVehicleRepository(FlitDbContext db) : IProcedureVehicleRepository
{
    public async Task UpsertAsync(ProcedureVehicleEntry entry, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, entry.TenantId, ct);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures.procedure_vehicles (
                id, tenant_id, procedure_instance_id, vehicle_subkind,
                license_plate, vin, created_by, updated_by, created_at, updated_at
            ) VALUES (
                @id, @tenant_id, @procedure_instance_id, @vehicle_subkind,
                @license_plate, @vin, @created_by, @updated_by, now(), now()
            )
            ON CONFLICT (procedure_instance_id) DO UPDATE SET
                vehicle_subkind = EXCLUDED.vehicle_subkind,
                license_plate = EXCLUDED.license_plate,
                vin = EXCLUDED.vin,
                updated_by = EXCLUDED.updated_by,
                updated_at = now()
            """;

        AddUuid(cmd, "id", entry.Id);
        AddUuid(cmd, "tenant_id", entry.TenantId);
        AddUuid(cmd, "procedure_instance_id", entry.ProcedureInstanceId);
        AddText(cmd, "vehicle_subkind", entry.VehicleSubkind);
        AddNullableText(cmd, "license_plate", entry.LicensePlate);
        AddNullableText(cmd, "vin", entry.Vin);
        AddUuid(cmd, "created_by", entry.CreatedBy);
        AddUuid(cmd, "updated_by", entry.UpdatedBy);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "23505")
        {
            // Sin tabla o sin constraint unique en instancia — ignorar en DEV parcial.
        }
    }

    public async Task<ProcedureVehicleEntry?> GetByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, procedure_instance_id, vehicle_subkind,
                   license_plate, vin, created_by, updated_by
            FROM procedures.procedure_vehicles
            WHERE tenant_id = @tenant_id
              AND procedure_instance_id = @procedure_instance_id
              AND deleted_at IS NULL
            LIMIT 1
            """;

        AddUuid(cmd, "tenant_id", tenantId);
        AddUuid(cmd, "procedure_instance_id", procedureInstanceId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return new ProcedureVehicleEntry(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetGuid(6),
                reader.GetGuid(7));
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return null;
        }
    }

    private static void AddUuid(NpgsqlCommand cmd, string name, Guid value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = value });

    private static void AddText(NpgsqlCommand cmd, string name, string value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text) { Value = value });

    private static void AddNullableText(NpgsqlCommand cmd, string name, string? value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value,
        });
}
