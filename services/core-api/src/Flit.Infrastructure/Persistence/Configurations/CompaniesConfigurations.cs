using Flit.Modules.Companies.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies", "companies");

        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(c => c.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.HasIndex(c => c.TenantId).IsUnique().HasDatabaseName("uq_companies_tenant");

        b.Property(c => c.Nit).HasColumnName("nit").IsRequired();
        b.HasIndex(c => c.Nit).IsUnique().HasDatabaseName("uq_companies_nit");

        b.Property(c => c.LegalName).HasColumnName("legal_name").IsRequired();
        b.Property(c => c.CommercialName).HasColumnName("commercial_name");
        b.Property(c => c.ModulesEnabledJson).HasColumnName("modules_enabled").HasColumnType("jsonb").IsRequired();

        b.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(c => c.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        b.Property(c => c.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid");
        b.Property(c => c.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        b.HasQueryFilter(c => c.DeletedAt == null);
    }
}

internal sealed class CompanyModuleConfigConfiguration : IEntityTypeConfiguration<CompanyModuleConfig>
{
    public void Configure(EntityTypeBuilder<CompanyModuleConfig> b)
    {
        b.ToTable("company_module_configs", "companies");

        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(c => c.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.Property(c => c.ModuleKey).HasColumnName("module_key").IsRequired();
        b.HasIndex(c => new { c.TenantId, c.ModuleKey }).IsUnique()
            .HasDatabaseName("uq_company_module_configs_tenant_module");

        b.Property(c => c.ConfigJson).HasColumnName("config").HasColumnType("jsonb").IsRequired();
        b.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(c => c.Version).HasColumnName("version").IsRequired();

        b.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(c => c.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        b.Property(c => c.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid");
        b.Property(c => c.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        b.HasQueryFilter(c => c.DeletedAt == null);
    }
}

internal sealed class SignatureWalletConfiguration : IEntityTypeConfiguration<SignatureWallet>
{
    public void Configure(EntityTypeBuilder<SignatureWallet> b)
    {
        b.ToTable("signature_wallets", "companies");

        b.HasKey(w => w.Id);
        b.Property(w => w.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(w => w.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.HasIndex(w => w.TenantId).IsUnique().HasDatabaseName("uq_signature_wallets_tenant");

        b.Property(w => w.Balance).HasColumnName("balance").IsRequired();
        b.Property(w => w.LowThreshold).HasColumnName("low_threshold").IsRequired();
        b.Property(w => w.AutoRecharge).HasColumnName("auto_recharge").IsRequired();

        b.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(w => w.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(w => w.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(w => w.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(w => w.DeletedAt).HasColumnName("deleted_at");
        b.Property(w => w.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid");
        b.Property(w => w.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        b.HasQueryFilter(w => w.DeletedAt == null);
    }
}

internal sealed class SignatureWalletMovementConfiguration : IEntityTypeConfiguration<SignatureWalletMovement>
{
    public void Configure(EntityTypeBuilder<SignatureWalletMovement> b)
    {
        b.ToTable("signature_wallet_movements", "companies");

        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(m => m.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.Property(m => m.WalletId).HasColumnName("wallet_id").HasColumnType("uuid").IsRequired();
        b.Property(m => m.Delta).HasColumnName("delta").IsRequired();
        b.Property(m => m.Reason).HasColumnName("reason").IsRequired();
        b.Property(m => m.BalanceAfter).HasColumnName("balance_after").IsRequired();
        b.Property(m => m.ProcedureInstanceId).HasColumnName("procedure_instance_id").HasColumnType("uuid");
        b.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(m => m.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
    }
}

internal sealed class VehicleOwnershipRuleConfiguration : IEntityTypeConfiguration<VehicleOwnershipRule>
{
    public void Configure(EntityTypeBuilder<VehicleOwnershipRule> b)
    {
        b.ToTable("vehicle_ownership_rules", "companies");

        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(r => r.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        b.Property(r => r.Name).HasColumnName("name").IsRequired();
        b.Property(r => r.RuleType).HasColumnName("rule_type").IsRequired();
        b.Property(r => r.ConditionJson).HasColumnName("condition").HasColumnType("jsonb").IsRequired();
        b.Property(r => r.Priority).HasColumnName("priority").IsRequired();
        b.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();

        b.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        b.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired();
        b.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        b.Property(r => r.DeletedBy).HasColumnName("deleted_by").HasColumnType("uuid");
        b.Property(r => r.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        b.HasQueryFilter(r => r.DeletedAt == null);
    }
}
