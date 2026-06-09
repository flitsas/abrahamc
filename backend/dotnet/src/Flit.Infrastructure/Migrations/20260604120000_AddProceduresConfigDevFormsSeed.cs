using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9409 — form_sections, form_fields y activaciones demo en PostgreSQL (idempotente).</summary>
public partial class AddProceduresConfigDevFormsSeed : Migration
{
    private const string UpResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.ProceduresConfigDevFormsSeed_up.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Datos funcionales DEV; rollback manual si aplica.
    }
}
