using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9693 — columna field_mapping en endpoint_catalog.</summary>
public partial class AddEndpointCatalogFieldMapping9693 : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.EndpointCatalogFieldMapping9693_up.sql";
    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.Tramites20.EndpointCatalogFieldMapping9693_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
