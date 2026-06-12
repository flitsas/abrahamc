using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9697 / #9699 — permisos modulo.ot.administrar y modulo.ot.gestionar-reglas.</summary>
public partial class AddOtPermissions9697 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.OtPermissions9697_up.sql";
    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.OtPermissions9697_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
