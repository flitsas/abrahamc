using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// Seed DEV idempotente — escenario Andina/Caribe + OT Bogotá/Medellín + mocks de integración.
/// Sin dependencias externas (Verifik/QX stub en BD). Ver ejemplos-datos.html.
/// Password: <c>FlitDev2026!</c>
/// </summary>
public partial class AddTramites20DevFunctionalSeed : Migration
{
    private const string UpResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_DevSeed_up.sql";
    private const string DownResource = "Flit.Infrastructure.Migrations.Sql.Tramites20.Tramites20_DevSeed_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
