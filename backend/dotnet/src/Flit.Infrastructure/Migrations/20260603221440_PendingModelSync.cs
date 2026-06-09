using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations
{
    /// <summary>
    /// Sincroniza el model snapshot con las entidades IDSecure en EF.
    /// El DDL real ya se aplicó en AddTramites20RuntimeLayer (scripts Tramites20_Runtime_up.sql).
    /// </summary>
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sin DDL: las tablas identity_verification y files.files existen vía SQL en la migración anterior.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin rollback: el down del DDL permanece en AddTramites20RuntimeLayer.
        }
    }
}
