using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations
{
    /// <summary>
    /// Sincroniza el snapshot EF con entidades IDSecure. Sin DDL: tablas ya creadas en AddTramites20RuntimeLayer.
    /// </summary>
    public partial class SyncIdSecureEfModelAfterTramites20Merge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
