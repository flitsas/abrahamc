using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9692 — permiso modulo.parametrizacion.gestionar.</summary>
public partial class AddParametrizacionPermissions9692 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.ParametrizacionPermissions9692_up.sql";
    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.ParametrizacionPermissions9692_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
