using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9427 — consultas RUNT/SIMIT/RUES para TRA_ESTANDAR en PostgreSQL (idempotente).</summary>
public partial class AddTraEstandarQueryConfigsSeed : Migration
{
    private const string UpResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.TraEstandarQueryConfigsSeed_up.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Datos funcionales DEV; rollback manual si aplica.
    }
}
