using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// HU #9408 #9409 #9410 — Núcleo de parametrización <c>procedures_config</c> (DDL 50):
/// familias/tipos/aristas/matriz, forms, consultas, documentos y activaciones por tenant.
/// Complementa <see cref="AddProceduresConfigRulesRgl01"/> (rules + endpoint_catalog).
/// Fuente: docs/designs/tramites-2.0/ddl/50-procedures_config.sql
/// </summary>
public partial class AddProceduresConfigParametrization50 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.AddProceduresConfigParametrization50_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.AddProceduresConfigParametrization50_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
