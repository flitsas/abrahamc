using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlDocumentTemplateRepository(FlitDbContext db) : IDocumentTemplateRepository
{
    public async Task<DocumentTemplateRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = await QueryTemplatesAsync(
            "SELECT id, code, name, document_type_id, scope, tenant_id, current_version, is_active, row_version FROM procedures_config.document_templates WHERE id = @id",
            cmd => AddUuid(cmd, "id", id),
            ct);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<DocumentTemplateRecord?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var list = await QueryTemplatesAsync(
            "SELECT id, code, name, document_type_id, scope, tenant_id, current_version, is_active, row_version FROM procedures_config.document_templates WHERE code = @code",
            cmd => AddText(cmd, "code", code),
            ct);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<IReadOnlyList<DocumentTemplateRecord>> ListAsync(CancellationToken ct = default) =>
        await QueryTemplatesAsync(
            "SELECT id, code, name, document_type_id, scope, tenant_id, current_version, is_active, row_version FROM procedures_config.document_templates ORDER BY code",
            _ => { },
            ct);

    public async Task<DocumentTemplateRecord> CreateAsync(DocumentTemplateWriteModel model, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var conn = await EnsureOpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.document_templates (
              id, code, name, document_type_id, scope, tenant_id, current_version, is_active,
              created_by, updated_by
            ) VALUES (
              @id, @code, @name, @document_type_id, @scope, @tenant_id, 0, true,
              @actor, @actor
            )
            """;
        AddUuid(cmd, "id", id);
        AddText(cmd, "code", model.Code);
        AddText(cmd, "name", model.Name);
        AddUuid(cmd, "document_type_id", model.DocumentTypeId);
        AddText(cmd, "scope", model.Scope);
        AddNullableUuid(cmd, "tenant_id", model.TenantId);
        AddUuid(cmd, "actor", model.ActorUserId);
        await cmd.ExecuteNonQueryAsync(ct);
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<DocumentTemplateVersionRecord?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default)
    {
        var list = await QueryVersionsAsync(
            """
            SELECT id, template_id, version, body_inline, marker_map::text, is_current, published_at, row_version
            FROM procedures_config.document_template_versions WHERE id = @id
            """,
            cmd => AddUuid(cmd, "id", versionId),
            ct);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<DocumentTemplateVersionRecord> AddVersionAsync(
        DocumentTemplateVersionWriteModel model,
        CancellationToken ct = default)
    {
        var conn = await EnsureOpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        int nextVersion;
        await using (var verCmd = conn.CreateCommand())
        {
            verCmd.Transaction = tx;
            verCmd.CommandText = """
                SELECT COALESCE(MAX(version), 0) + 1 FROM procedures_config.document_template_versions
                WHERE template_id = @template_id
                """;
            AddUuid(verCmd, "template_id", model.TemplateId);
            nextVersion = Convert.ToInt32(await verCmd.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        }

        var versionId = Guid.NewGuid();
        await using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO procedures_config.document_template_versions (
                  id, template_id, version, body_inline, marker_map, is_current, created_by, updated_by
                ) VALUES (
                  @id, @template_id, @version, @body_inline, @marker_map::jsonb, false, @actor, @actor
                )
                """;
            AddUuid(ins, "id", versionId);
            AddUuid(ins, "template_id", model.TemplateId);
            ins.Parameters.Add(new NpgsqlParameter("version", NpgsqlDbType.Integer) { Value = nextVersion });
            AddText(ins, "body_inline", model.BodyInline);
            AddJson(ins, "marker_map", model.MarkerMap.GetRawText());
            AddUuid(ins, "actor", model.ActorUserId);
            await ins.ExecuteNonQueryAsync(ct);
        }

        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE procedures_config.document_templates
                SET current_version = @version, updated_at = now(), updated_by = @actor
                WHERE id = @template_id
                """;
            upd.Parameters.Add(new NpgsqlParameter("version", NpgsqlDbType.Integer) { Value = nextVersion });
            AddUuid(upd, "actor", model.ActorUserId);
            AddUuid(upd, "template_id", model.TemplateId);
            await upd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return (await GetVersionByIdAsync(versionId, ct))!;
    }

    public async Task<ResultPublish> PublishVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        var version = await GetVersionByIdAsync(versionId, ct);
        if (version is null)
            return ResultPublish.NotFound;
        if (version.PublishedAt is not null)
            return ResultPublish.AlreadyPublished;

        var conn = await EnsureOpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = """
                UPDATE procedures_config.document_template_versions
                SET is_current = false, updated_at = now(), updated_by = @actor
                WHERE template_id = (SELECT template_id FROM procedures_config.document_template_versions WHERE id = @id)
                """;
            AddUuid(clear, "id", versionId);
            AddUuid(clear, "actor", actorUserId);
            await clear.ExecuteNonQueryAsync(ct);
        }

        await using (var pub = conn.CreateCommand())
        {
            pub.Transaction = tx;
            pub.CommandText = """
                UPDATE procedures_config.document_template_versions
                SET is_current = true, published_at = now(), updated_at = now(), updated_by = @actor
                WHERE id = @id AND published_at IS NULL
                """;
            AddUuid(pub, "id", versionId);
            AddUuid(pub, "actor", actorUserId);
            var rows = await pub.ExecuteNonQueryAsync(ct);
            if (rows == 0)
            {
                await tx.RollbackAsync(ct);
                return ResultPublish.AlreadyPublished;
            }
        }

        await tx.CommitAsync(ct);
        return ResultPublish.Ok;
    }

    public async Task<UpdateMarkerMapResult> TryUpdateMarkerMapAsync(
        Guid versionId,
        JsonElement markerMap,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var version = await GetVersionByIdAsync(versionId, ct);
        if (version is null)
            return UpdateMarkerMapResult.NotFound;
        if (version.PublishedAt is not null)
            return UpdateMarkerMapResult.ImmutableVersion;

        var conn = await EnsureOpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.document_template_versions
            SET marker_map = @marker_map::jsonb, updated_at = now(), updated_by = @actor
            WHERE id = @id AND published_at IS NULL
            """;
        AddUuid(cmd, "id", versionId);
        AddJson(cmd, "marker_map", markerMap.GetRawText());
        AddUuid(cmd, "actor", actorUserId);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0 ? UpdateMarkerMapResult.Ok : UpdateMarkerMapResult.ImmutableVersion;
    }

    private async Task<List<DocumentTemplateRecord>> QueryTemplatesAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        var conn = await EnsureOpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);
        var list = new List<DocumentTemplateRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DocumentTemplateRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.GetInt32(6),
                reader.GetBoolean(7),
                reader.GetInt32(8)));
        }

        return list;
    }

    private async Task<List<DocumentTemplateVersionRecord>> QueryVersionsAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        var conn = await EnsureOpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);
        var list = new List<DocumentTemplateVersionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var markerJson = reader.GetString(4);
            using var markerDoc = JsonDocument.Parse(markerJson);
            list.Add(new DocumentTemplateVersionRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                markerDoc.RootElement.Clone(),
                reader.GetBoolean(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetInt32(7)));
        }

        return list;
    }

    /// <summary>
    /// Abre la conexión compartida del <see cref="FlitDbContext"/> sin tomar ownership:
    /// no usar <c>await using</c> sobre el valor devuelto (el contexto la dispone al final del scope).
    /// </summary>
    private async Task<NpgsqlConnection> EnsureOpenAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }

    private static void AddUuid(NpgsqlCommand cmd, string name, Guid value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = value });

    private static void AddNullableUuid(NpgsqlCommand cmd, string name, Guid? value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value });

    private static void AddText(NpgsqlCommand cmd, string name, string value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text) { Value = value });

    private static void AddJson(NpgsqlCommand cmd, string name, string json) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = json });
}
