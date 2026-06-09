using Flit.Modules.Companies.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
#pragma warning disable CA1812

namespace Flit.Infrastructure.Persistence.Configurations;

internal sealed class RuntSyncLogEntryConfiguration : IEntityTypeConfiguration<RuntSyncLogEntry>
{
    public void Configure(EntityTypeBuilder<RuntSyncLogEntry> b)
    {
        b.ToTable("runt_sync_log", "integrations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").IsRequired();
        b.Property(x => x.Operation).HasColumnName("operation").IsRequired();
        b.Property(x => x.Outcome).HasColumnName("outcome").IsRequired();
        b.Property(x => x.FailoverFrom).HasColumnName("failover_from");
        b.Property(x => x.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.SyncedAt).HasColumnName("synced_at").IsRequired();
    }
}

internal sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> b)
    {
        b.ToTable("webhook_events", "integrations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.TrafficAgencyId).HasColumnName("traffic_agency_id").HasColumnType("uuid");
        b.Property(x => x.Direction).HasColumnName("direction").IsRequired();
        b.Property(x => x.EventType).HasColumnName("event_type").IsRequired();
        b.Property(x => x.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Signature).HasColumnName("signature");
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").IsRequired();
        b.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("uq_webhook_events_idempotency");
    }
}

internal sealed class ProcedureQueryResultSnapshotConfiguration
    : IEntityTypeConfiguration<ProcedureQueryResultSnapshot>
{
    public void Configure(EntityTypeBuilder<ProcedureQueryResultSnapshot> b)
    {
        b.ToTable("procedure_query_results", "procedures");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.ProcedureInstanceId).HasColumnName("procedure_instance_id").HasColumnType("uuid").IsRequired();
        b.Property(x => x.QueryConnectorCode).HasColumnName("query_connector_code").IsRequired();
        b.Property(x => x.EdgeRole).HasColumnName("edge_role");
        b.Property(x => x.Source).HasColumnName("source").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").IsRequired();
        b.Property(x => x.ResultJson).HasColumnName("result").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.RequestedAt).HasColumnName("requested_at").IsRequired();
        b.Property(x => x.RespondedAt).HasColumnName("responded_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
    }
}
