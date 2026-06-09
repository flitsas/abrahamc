using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// HU #9438 RGL-02 — <c>integrations.endpoint_call_log</c> (bitácora de invocaciones desde reglas).
/// </summary>
public partial class AddIntegrationsEndpointCallLogRgl02 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.AddIntegrationsEndpointCallLogRgl02_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.AddIntegrationsEndpointCallLogRgl02_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
