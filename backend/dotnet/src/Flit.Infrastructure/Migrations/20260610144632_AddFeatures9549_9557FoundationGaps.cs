using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatures9549_9557FoundationGaps : Migration
    {
        private const string UpResource =
            "Flit.Infrastructure.Migrations.Sql.Tramites20.Features9549_9557_FoundationGaps_up.sql";
        private const string DownResource =
            "Flit.Infrastructure.Migrations.Sql.Tramites20.Features9549_9557_FoundationGaps_down.sql";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, UpResource);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            SqlMigrationHelper.ApplyEmbeddedSql(migrationBuilder, DownResource);
    }
}
