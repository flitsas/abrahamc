using Flit.Modules.Companies.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

internal sealed class OtQxIntegrationConfiguration : IEntityTypeConfiguration<OtQxIntegration>
{
    public void Configure(EntityTypeBuilder<OtQxIntegration> b)
    {
        b.ToTable("ot_qx_integrations", "ot");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TrafficAgencyId).HasColumnName("traffic_agency_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.Mode).HasColumnName("mode").IsRequired();
        b.Property(x => x.CallbackUrl).HasColumnName("callback_url");
        b.Property(x => x.AuthConfigJson).HasColumnName("auth_config").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        b.HasIndex(x => x.TrafficAgencyId).IsUnique().HasDatabaseName("uq_ot_qx_integrations_agency");
    }
}

internal sealed class OtRuleConfiguration : IEntityTypeConfiguration<OtRule>
{
    public void Configure(EntityTypeBuilder<OtRule> b)
    {
        b.ToTable("ot_rules", "ot");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TrafficAgencyId).HasColumnName("traffic_agency_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.TriggerEvent).HasColumnName("trigger_event").IsRequired();
        b.Property(x => x.ConditionTreeJson).HasColumnName("condition_tree").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ActionsJson).HasColumnName("actions").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.ValidFrom).HasColumnName("valid_from");
        b.Property(x => x.ValidUntil).HasColumnName("valid_until");
        b.Property(x => x.SchemaVersion).HasColumnName("schema_version").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        b.HasIndex(x => new { x.TrafficAgencyId, x.IsActive })
            .HasDatabaseName("ix_ot_rules_traffic_agency_id_active")
            .HasFilter("deleted_at IS NULL");
    }
}

internal sealed class OtConsolidatedDocOrderConfiguration : IEntityTypeConfiguration<OtConsolidatedDocOrder>
{
    public void Configure(EntityTypeBuilder<OtConsolidatedDocOrder> b)
    {
        b.ToTable("ot_consolidated_doc_orders", "ot");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TrafficAgencyId).HasColumnName("traffic_agency_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
    }
}

internal sealed class OtConsolidatedDocOrderItemConfiguration : IEntityTypeConfiguration<OtConsolidatedDocOrderItem>
{
    public void Configure(EntityTypeBuilder<OtConsolidatedDocOrderItem> b)
    {
        b.ToTable("ot_consolidated_doc_order_items", "ot");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TrafficAgencyId).HasColumnName("traffic_agency_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.OrderId).HasColumnName("order_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.ProcedureDocumentCatalogId).HasColumnName("procedure_document_catalog_id").HasColumnType("uuid");
        b.Property(x => x.CustomLabel).HasColumnName("custom_label");
        b.Property(x => x.Position).HasColumnName("position").IsRequired();
        b.Property(x => x.Source).HasColumnName("source").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        b.HasIndex(x => new { x.OrderId, x.Position }).IsUnique().HasDatabaseName("uq_ot_cons_items_order_position");
    }
}
