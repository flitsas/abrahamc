using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// Features #9370 #9466 #9381 #9383 #9378 #9379 — Foundation Trámites 2.0 (DDL 00/10/20/25/30/40).
/// Retira tablas shell legacy en <c>identity</c> y aplica el modelo canónico multi-tenant.
/// Fuente: docs/designs/tramites-2.0/ddl/
/// </summary>
public partial class AddTramites20Foundation : Migration
{
    private const string UpResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_Foundation_up.sql";
    private const string DownResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_Foundation_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
