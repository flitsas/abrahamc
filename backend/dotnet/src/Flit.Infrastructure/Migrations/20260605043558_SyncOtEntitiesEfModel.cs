using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncOtEntitiesEfModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model-only sync migration.
            // Tables ot.ot_qx_integrations, ot.ot_rules and integrations.webhook_events were
            // already created by the embedded SQL in AddTramites20RuntimeLayer.
            // This migration only updates the EF Core ModelSnapshot so that
            // PendingModelChangesWarning is resolved without re-creating existing objects.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No DDL to revert — tables are owned by AddTramites20RuntimeLayer SQL scripts.
        }
    }
}
