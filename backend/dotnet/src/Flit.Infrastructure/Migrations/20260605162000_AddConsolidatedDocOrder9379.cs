using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// HU #9460 (Feature #9379) — FK ot_consolidated_doc_order_items → procedure_document_catalog (#9409)
/// + seed orden DEV Bogotá. AC1 índice uq activo por OT (Foundation); AC2 lazy/seed controlado.
/// </summary>
public partial class AddConsolidatedDocOrder9379 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_ConsolidatedDocOrder9379_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_ConsolidatedDocOrder9379_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
