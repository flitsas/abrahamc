using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// Features #9467 #9408 #9469 #9369 — Runtime: integrations (70), identity_verification (75/76),
/// procedures (80), dashboard (90).
/// </summary>
public partial class AddTramites20RuntimeLayer : Migration
{
    private const string UpResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_Runtime_up.sql";
    private const string DownResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_Runtime_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
