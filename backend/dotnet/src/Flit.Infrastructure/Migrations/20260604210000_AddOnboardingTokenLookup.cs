using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>HU #9418 — función SECURITY DEFINER para preview/activate de onboarding.</summary>
public partial class AddOnboardingTokenLookup : Migration
{
    private const string UpResource =
        "Flit.Infrastructure.Migrations.Sql.AddOnboardingTokenLookup_up.sql";

    private const string DownResource =
        "Flit.Infrastructure.Migrations.Sql.AddOnboardingTokenLookup_down.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
}
