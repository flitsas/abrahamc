using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Flit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFlitShell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "rbac");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "identity_credentials",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    cambiado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_credentials", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "identity_refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    jti = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_refresh_tokens", x => x.jti);
                });

            migrationBuilder.CreateTable(
                name: "identity_users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email_verificado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    documento_tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    documento_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombres = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fecha_nacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    organismo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    bloqueado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_login = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    habeas_data_consentimiento = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    habeas_data_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    habeas_data_politica_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu_items",
                schema: "rbac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    frontend_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_separator = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_mi_parent",
                        column: x => x.parent_id,
                        principalSchema: "rbac",
                        principalTable: "menu_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_masked = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    procedure_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    body_excerpt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_delivery", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "rbac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "rbac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_inconsistencies",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cognito_sub = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    detail = table.Column<string>(type: "text", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_inconsistencies", x => x.id);
                    table.CheckConstraint("chk_sync_type", "type IN ('COGNITO_ORPHAN','DB_ORPHAN','STATUS_DESYNCED')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cognito_sub = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    document_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    document_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mfa_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    mfa_secret_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    mfa_secret_nonce = table.Column<byte[]>(type: "bytea", nullable: true),
                    mfa_secret_key_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mfa_enabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("chk_users_status", "status IN ('ACTIVE','INACTIVE','BLOCKED','DELETED')");
                    table.ForeignKey(
                        name: "fk_users_created_by",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "role_menu_items",
                schema: "rbac",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_menu_items", x => new { x.role_id, x.menu_item_id });
                    table.ForeignKey(
                        name: "fk_rmi_mi",
                        column: x => x.menu_item_id,
                        principalSchema: "rbac",
                        principalTable: "menu_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rmi_role",
                        column: x => x.role_id,
                        principalSchema: "rbac",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "rbac",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_rp_permission",
                        column: x => x.permission_id,
                        principalSchema: "rbac",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rp_role",
                        column: x => x.role_id,
                        principalSchema: "rbac",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_prt_user",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_audit_log",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(50)", maxLength: 50, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    executed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_ual_user",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "rbac",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_ur_role",
                        column: x => x.role_id,
                        principalSchema: "rbac",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ur_user",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_refresh_tokens_expires_at",
                schema: "identity",
                table: "identity_refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_identity_users_activo",
                schema: "identity",
                table: "identity_users",
                column: "activo");

            migrationBuilder.CreateIndex(
                name: "ix_identity_users_documento",
                schema: "identity",
                table: "identity_users",
                columns: new[] { "documento_tipo", "documento_numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identity_users_email",
                schema: "identity",
                table: "identity_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_items_code",
                schema: "rbac",
                table: "menu_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_items_parent",
                schema: "rbac",
                table: "menu_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_items_sort",
                schema: "rbac",
                table: "menu_items",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_created_at",
                schema: "notifications",
                table: "notification_delivery",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_procedure_id",
                schema: "notifications",
                table: "notification_delivery",
                column: "procedure_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_recipient_user_id",
                schema: "notifications",
                table: "notification_delivery",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prt_token_hash",
                schema: "identity",
                table: "password_reset_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_prt_user_id",
                schema: "identity",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_code",
                schema: "rbac",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permissions_module",
                schema: "rbac",
                table: "permissions",
                column: "module");

            migrationBuilder.CreateIndex(
                name: "IX_role_menu_items_menu_item_id",
                schema: "rbac",
                table: "role_menu_items",
                column: "menu_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                schema: "rbac",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_code",
                schema: "rbac",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sync_type_unresolved",
                schema: "identity",
                table: "sync_inconsistencies",
                column: "type",
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ual_event",
                schema: "identity",
                table: "user_audit_log",
                column: "event");

            migrationBuilder.CreateIndex(
                name: "ix_ual_occurred_at",
                schema: "identity",
                table: "user_audit_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_ual_user_id",
                schema: "identity",
                table: "user_audit_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                schema: "rbac",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_created_by_user_id",
                schema: "identity",
                table: "users",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_cognito_sub",
                schema: "identity",
                table: "users",
                column: "cognito_sub",
                unique: true,
                filter: "cognito_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_document",
                schema: "identity",
                table: "users",
                columns: new[] { "document_type", "document_number" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_email_active",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                schema: "identity",
                table: "users",
                column: "status",
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "identity_refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "identity_users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "notification_delivery",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_menu_items",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "sync_inconsistencies",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_audit_log",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "menu_items",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "rbac");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
