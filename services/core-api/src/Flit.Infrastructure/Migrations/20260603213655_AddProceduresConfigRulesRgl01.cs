using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// HU #9437 RGL-01 — Schema <c>procedures_config</c> con <c>rules</c>, <c>endpoint_catalog</c>
/// y validadores JSONB (<c>is_valid_rule_condition</c> / <c>is_valid_rule_actions</c>).
/// Fuente: docs/designs/tramites-2.0/ddl/50-procedures_config.sql, ADR-0011.
/// </summary>
public partial class AddProceduresConfigRulesRgl01 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.AddProceduresConfigRulesRgl01_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.AddProceduresConfigRulesRgl01_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
