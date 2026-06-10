using System.Data;
using Flit.Infrastructure.Persistence;
using Flit.Infrastructure.Persistence.SeedData;
using Flit.Modules.ProceduresConfig.Adapters;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.Modules.Rbac.Ports;
using Flit.Modules.Users.Adapters;
using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;
using Flit.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Flit.Api.Endpoints;

/// <summary>
/// Endpoints SOLO para entorno Development. Permiten levantar la demo local
/// sin AWS Cognito ni Verifik.
///
/// Requiere migraciones EF aplicadas (<c>pnpm migrate</c> o <c>MigrateAsync</c> al arrancar).
/// Si faltan roles/permisos, aplica FlitV2Seeds antes de asignar ADMIN.
///
/// Endpoint: POST /api/v1/dev/seed-admin?email=admin@flit.io
/// Idempotente: si ya existe, garantiza el rol ADMIN.
/// </summary>
public static class DevSeedEndpoints
{
    public static void MapDevSeedEndpoints(this IEndpointRouteBuilder app, IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
            return;

        app.MapPost("/api/v1/dev/seed-admin", async (
            string? email,
            FlitDbContext db,
            IUsersRepository usersRepo,
            IRolesRepository rolesRepo,
            IRoleAssignmentsRepository assignRepo,
            IPermissionsCache cache,
            IClock clock,
            CancellationToken ct) =>
        {
            var targetEmail = (email ?? "admin@flit.io").Trim().ToLowerInvariant();
            var sub = StubCognitoDirectory.SubForEmail(targetEmail);

            // 1. Usuario
            var user = await usersRepo.GetByEmailAsync(targetEmail, ct);
            if (user is null)
            {
                user = User.Create(
                    email: targetEmail,
                    fullName: "Administrador Demo",
                    documentType: "CC",
                    documentNumber: "1000000000",
                    phone: "3000000000",
                    createdByUserId: null,
                    now: clock.UtcNow);
                user.LinkCognitoSub(sub, clock.UtcNow);
                await usersRepo.AddAsync(user, ct);
            }

            // 2. Rol ADMIN (seeds SQL o dev endpoint)
            var adminRole = await rolesRepo.GetByCodeAsync("ADMIN", ct);
            if (adminRole is null)
            {
                try
                {
                    await FlitShellSeedData.ApplyAllAsync(db, ct);
                    adminRole = await rolesRepo.GetByCodeAsync("ADMIN", ct);
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
                {
                    return Results.Problem(
                        "Schema no encontrado. Ejecuta pnpm migrate o levanta core-api con ConnectionStrings:Core.",
                        statusCode: 500);
                }
            }

            if (adminRole is null)
            {
                return Results.Problem(
                    "Rol ADMIN no encontrado tras aplicar seeds. Verifica migraciones EF y FlitV2Seeds.",
                    statusCode: 500);
            }

            // 3. Assignment user -> ADMIN (idempotente)
            var currentRoles = await rolesRepo.GetByUserAsync(user.Id, ct);
            if (currentRoles.All(r => r.Id != adminRole.Id))
            {
                await assignRepo.AssignRoleToUserAsync(
                    user.Id, adminRole.Id, assignedByUserId: null, ct);
            }

            // 4. Invalidar cache de permisos
            await cache.InvalidateAsync(user.Id, ct);

            return Results.Ok(new
            {
                userId = user.Id,
                email = user.Email,
                cognitoSub = sub,
                role = "ADMIN",
                message = "Usuario admin listo. Login con cualquier password " +
                          "(StubCognitoDirectory acepta todas en dev).",
            });
        })
        .WithName("DevSeedAdmin")
        .WithTags("Dev");

        // HU #9443 DOC-02 — orden consolidado demo para Postman / pruebas locales
        app.MapPost("/api/v1/dev/seed-doc02-consolidated-order", async (
            SeedDoc02ConsolidatedOrderRequest? req,
            IServiceProvider services,
            CancellationToken ct) =>
        {
            var inMemoryOrderRepo = services.GetService<InMemoryOtConsolidatedDocOrderRepository>();
            var db = services.GetService<FlitDbContext>();
            var trafficAgencyId = req?.TrafficAgencyId
                ?? InMemoryProceduresConfigReadRepository.DemoOtBogotaId;
            var actorId = ProceduresConfigEndpoints.DefaultActorUserId;

            if (inMemoryOrderRepo is not null)
            {
                var docCc = req?.DocumentTypeIdFirst
                    ?? Guid.Parse("00000000-0000-7000-8001-000000000001");
                var docNit = req?.DocumentTypeIdSecond
                    ?? Guid.Parse("00000000-0000-7000-8001-000000000002");
                var orderId = Guid.NewGuid();

                var items = new List<OtConsolidatedDocOrderItemRecord>
                {
                    new(Guid.NewGuid(), orderId, trafficAgencyId, docCc, null, 1, "global"),
                };
                if (req?.SingleItemOnly != true)
                {
                    items.Add(new OtConsolidatedDocOrderItemRecord(
                        Guid.NewGuid(), orderId, trafficAgencyId, docNit, null, 2, "global"));
                }

                inMemoryOrderRepo.SeedOrder(
                    new OtConsolidatedDocOrderRecord(orderId, trafficAgencyId, 1, true),
                    items);

                return Results.Ok(new SeedDoc02ConsolidatedOrderResponse(
                    trafficAgencyId,
                    orderId,
                    docCc,
                    docNit,
                    "in-memory",
                    "Orden consolidado demo sembrado (FUN position=1, CARTA position=2)."));
            }

            if (db is null)
            {
                return Results.Problem(
                    "No hay repositorio in-memory ni DbContext. Verifica ConnectionStrings:Core.",
                    statusCode: 500);
            }

            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            Guid docCcId;
            Guid docNitId;
            await using (var lookup = conn.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT id, code FROM catalogs.document_types
                    WHERE code IN ('CC', 'NIT') ORDER BY code
                    """;
                await using var reader = await lookup.ExecuteReaderAsync(ct);
                Guid? cc = null;
                Guid? nit = null;
                while (await reader.ReadAsync(ct))
                {
                    if (reader.GetString(1) == "CC")
                        cc = reader.GetGuid(0);
                    else
                        nit = reader.GetGuid(0);
                }

                if (cc is null || nit is null)
                {
                    return Results.Problem(
                        "Catálogo catalogs.document_types sin CC/NIT. Ejecuta pnpm migrate.",
                        statusCode: 500);
                }

                docCcId = req?.DocumentTypeIdFirst ?? cc.Value;
                docNitId = req?.DocumentTypeIdSecond ?? nit.Value;
            }

            await using var tx = await conn.BeginTransactionAsync(ct);
            var orderIdPg = Guid.NewGuid();

            await using (var deactivate = conn.CreateCommand())
            {
                deactivate.Transaction = tx;
                deactivate.CommandText = """
                    UPDATE ot.ot_consolidated_doc_orders
                    SET is_active = false, updated_at = now(), updated_by = @actor
                    WHERE traffic_agency_id = @agency AND is_active = true AND deleted_at IS NULL
                    """;
                deactivate.Parameters.Add(new NpgsqlParameter("agency", NpgsqlDbType.Uuid) { Value = trafficAgencyId });
                deactivate.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = actorId });
                await deactivate.ExecuteNonQueryAsync(ct);
            }

            await using (var insertOrder = conn.CreateCommand())
            {
                insertOrder.Transaction = tx;
                insertOrder.CommandText = """
                    INSERT INTO ot.ot_consolidated_doc_orders (
                      id, traffic_agency_id, version, is_active, created_by, updated_by
                    ) VALUES (@id, @agency, 1, true, @actor, @actor)
                    """;
                insertOrder.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = orderIdPg });
                insertOrder.Parameters.Add(new NpgsqlParameter("agency", NpgsqlDbType.Uuid) { Value = trafficAgencyId });
                insertOrder.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = actorId });
                await insertOrder.ExecuteNonQueryAsync(ct);
            }

            await using (var insertItems = conn.CreateCommand())
            {
                insertItems.Transaction = tx;
                if (req?.SingleItemOnly == true)
                {
                    insertItems.CommandText = """
                        INSERT INTO ot.ot_consolidated_doc_order_items (
                          id, traffic_agency_id, order_id, document_type_id, position, source, created_by, updated_by
                        ) VALUES (@id1, @agency, @order, @doc1, 1, 'global', @actor, @actor)
                        """;
                    insertItems.Parameters.Add(new NpgsqlParameter("id1", NpgsqlDbType.Uuid) { Value = Guid.NewGuid() });
                    insertItems.Parameters.Add(new NpgsqlParameter("agency", NpgsqlDbType.Uuid) { Value = trafficAgencyId });
                    insertItems.Parameters.Add(new NpgsqlParameter("order", NpgsqlDbType.Uuid) { Value = orderIdPg });
                    insertItems.Parameters.Add(new NpgsqlParameter("doc1", NpgsqlDbType.Uuid) { Value = docCcId });
                    insertItems.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = actorId });
                }
                else
                {
                    insertItems.CommandText = """
                        INSERT INTO ot.ot_consolidated_doc_order_items (
                          id, traffic_agency_id, order_id, document_type_id, position, source, created_by, updated_by
                        ) VALUES
                          (@id1, @agency, @order, @doc1, 1, 'global', @actor, @actor),
                          (@id2, @agency, @order, @doc2, 2, 'global', @actor, @actor)
                        """;
                    insertItems.Parameters.Add(new NpgsqlParameter("id1", NpgsqlDbType.Uuid) { Value = Guid.NewGuid() });
                    insertItems.Parameters.Add(new NpgsqlParameter("id2", NpgsqlDbType.Uuid) { Value = Guid.NewGuid() });
                    insertItems.Parameters.Add(new NpgsqlParameter("agency", NpgsqlDbType.Uuid) { Value = trafficAgencyId });
                    insertItems.Parameters.Add(new NpgsqlParameter("order", NpgsqlDbType.Uuid) { Value = orderIdPg });
                    insertItems.Parameters.Add(new NpgsqlParameter("doc1", NpgsqlDbType.Uuid) { Value = docCcId });
                    insertItems.Parameters.Add(new NpgsqlParameter("doc2", NpgsqlDbType.Uuid) { Value = docNitId });
                    insertItems.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = actorId });
                }

                await insertItems.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            return Results.Ok(new SeedDoc02ConsolidatedOrderResponse(
                trafficAgencyId,
                orderIdPg,
                docCcId,
                docNitId,
                "postgres",
                "Orden consolidado demo sembrado en ot.ot_consolidated_doc_orders (+items)."));
        })
        .WithName("DevSeedDoc02ConsolidatedOrder")
        .WithTags("Dev");

        // HU #9443 DOC-02 — descarga local del PDF (cache in-memory del proceso; no MinIO aún)
        app.MapGet("/api/v1/dev/files/{fileId:guid}/download", async (
            Guid fileId,
            IServiceProvider services,
            CancellationToken ct) =>
        {
            var fileStore = services.GetService<IProcedurePdfFileStore>();
            if (fileStore is null)
            {
                return Results.Problem(
                    "IProcedurePdfFileStore no registrado.",
                    statusCode: 500);
            }

            var bytes = await fileStore.TryGetAsync(fileId, ct);
            if (bytes is null || bytes.Length == 0)
            {
                return Results.NotFound(new
                {
                    error = "PDF no encontrado. Si reiniciaste Flit.Api, el binario se pierde (cache in-memory). Vuelve a ejecutar package-consolidated.",
                    fileId,
                });
            }

            return Results.File(bytes, "application/pdf", $"consolidado-{fileId:N}.pdf");
        })
        .WithName("DevDownloadProcedurePdf")
        .WithTags("Dev");
    }

    public sealed record SeedDoc02ConsolidatedOrderRequest(
        Guid? TrafficAgencyId = null,
        Guid? DocumentTypeIdFirst = null,
        Guid? DocumentTypeIdSecond = null,
        bool SingleItemOnly = false);

    public sealed record SeedDoc02ConsolidatedOrderResponse(
        Guid TrafficAgencyId,
        Guid OrderId,
        Guid DocumentTypeIdFirst,
        Guid DocumentTypeIdSecond,
        string Storage,
        string Message);
}
