using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>Arista documentos + migración de required_documents al paso único por trámite.</summary>
public partial class AddTramites20DocumentosEdge : Migration
{
    private const string UpResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_DocumentosEdge_up.sql";
    private const string DownResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_DocumentosEdge_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
