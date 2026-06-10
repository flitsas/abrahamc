using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// Backfill modulo.ot.operador / superadmin en roles maestros (BDs sembradas sin slugs OT completos).
/// </summary>
public partial class AddOtRolePermissionsBackfill : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.OtRolePermissionsBackfill_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.OtRolePermissionsBackfill_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
