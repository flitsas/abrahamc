using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// HU #9454 (Feature #9378 OT-01) — Semilla catálogo OT (~359) en ot.traffic_agencies.
/// Fuente: Persistence/SeedData/traffic_secretaries.csv.
/// </summary>
public partial class AddOtTrafficAgenciesCatalogSeed : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.OtTrafficAgenciesCatalogSeed_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.OtTrafficAgenciesCatalogSeed_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
