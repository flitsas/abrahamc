using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations;

/// <summary>
/// Sincroniza el snapshot EF con entidades OT consolidado (#9379) ya materializadas
/// vía SQL en Tramites20_Foundation / AddConsolidatedDocOrder9379. Sin DDL en Up.
/// </summary>
public partial class PendingModelSyncVpsJun05 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tablas ot.ot_consolidated_doc_orders / ot_consolidated_doc_order_items ya existen en DEV/VPS.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
