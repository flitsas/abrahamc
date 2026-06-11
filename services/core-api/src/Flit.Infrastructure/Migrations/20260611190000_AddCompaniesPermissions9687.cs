using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9687 — permisos modulo.companias.ver y modulo.companias.gestionar.</summary>
public partial class AddCompaniesPermissions9687 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.CompaniesPermissions9687_up.sql";
    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.CompaniesPermissions9687_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
