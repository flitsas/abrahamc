using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>Feature #9732 — grilla runtime, async queries, copropiedad (#10079/#10080/#10081).</summary>
public partial class AddProcedureRuntime9732 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.ProcedureRuntime9732_up.sql";
    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.ProcedureRuntime9732_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
