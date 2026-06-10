using Flit.Modules.Users.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configurations para el schema `identity` (ADR-0010).
/// Validado contra docs/sql/flit-v2-initial-schema.sql.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", "identity");

        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        b.Property(u => u.CognitoSub).HasColumnName("cognito_sub").HasMaxLength(64);
        // cognito_sub UNIQUE NOT NULL en SQL canónico, pero en BD permitimos NULL transitorio
        // durante la creación local-first (insert -> Cognito -> update sub).
        b.HasIndex(u => u.CognitoSub).IsUnique()
            .HasFilter("cognito_sub IS NOT NULL")
            .HasDatabaseName("ix_users_cognito_sub");

        b.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.HasIndex(u => u.Email).IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_users_email_active");

        b.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        b.Property(u => u.DocumentType).HasColumnName("document_type").HasMaxLength(10);
        b.Property(u => u.DocumentNumber).HasColumnName("document_number").HasMaxLength(20);
        b.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(20);

        b.Property(u => u.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();
        b.HasIndex(u => u.Status)
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_users_status");
        b.ToTable(t => t.HasCheckConstraint(
            "chk_users_status",
            "status IN ('ACTIVE','INACTIVE','BLOCKED','DELETED')"));

        b.Property(u => u.MfaEnabled).HasColumnName("mfa_enabled").IsRequired().HasDefaultValue(false);
        // MfaSecret VO aplanado en 3 columnas opcionales.
        b.OwnsOne(u => u.MfaSecret, ms =>
        {
            ms.Property(s => s.Ciphertext).HasColumnName("mfa_secret_ciphertext");
            ms.Property(s => s.Nonce).HasColumnName("mfa_secret_nonce");
            ms.Property(s => s.KeyId).HasColumnName("mfa_secret_key_id").HasMaxLength(64);
        });

        b.Property(u => u.MfaEnabledAt).HasColumnName("mfa_enabled_at");
        b.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        b.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(u => u.DeletedAt).HasColumnName("deleted_at");
        b.Property(u => u.CreatedByUserId).HasColumnName("created_by_user_id");

        // Self FK created_by_user_id → users(id) ON DELETE SET NULL
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.CreatedByUserId)
            .HasConstraintName("fk_users_created_by")
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(u => new { u.DocumentType, u.DocumentNumber })
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_users_document");

        // Soft delete: filtra DeletedAt IS NULL en queries por defecto.
        b.HasQueryFilter(u => u.DeletedAt == null);
    }
}

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("password_reset_tokens", "identity");

        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        b.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        b.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
        b.Property(t => t.UsedAt).HasColumnName("used_at");
        b.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .HasConstraintName("fk_prt_user")
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(t => t.TokenHash).HasDatabaseName("ix_prt_token_hash");
        b.HasIndex(t => t.UserId).HasDatabaseName("ix_prt_user_id");
    }
}

internal sealed class UserAuditLogConfiguration : IEntityTypeConfiguration<UserAuditLog>
{
    public void Configure(EntityTypeBuilder<UserAuditLog> b)
    {
        b.ToTable("user_audit_log", "identity");

        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(l => l.UserId).HasColumnName("user_id").IsRequired();
        b.Property(l => l.Event).HasColumnName("event").HasMaxLength(50).IsRequired();
        b.Property(l => l.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        b.Property(l => l.ExecutedByUserId).HasColumnName("executed_by_user_id");
        // string -> varchar(45) suficiente para IPv6 + IPv4-mapped. Postgres
        // tiene tipo nativo `inet` pero EF Core sin Npgsql.ValueConverter no
        // mapea string a inet directamente. Para queries por subred usar inet
        // explicito con un converter (Fase post-MVP).
        b.Property(l => l.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        b.Property(l => l.UserAgent).HasColumnName("user_agent");
        b.Property(l => l.OccurredAt).HasColumnName("occurred_at").IsRequired();

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .HasConstraintName("fk_ual_user")
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(l => l.UserId).HasDatabaseName("ix_ual_user_id");
        b.HasIndex(l => l.Event).HasDatabaseName("ix_ual_event");
        b.HasIndex(l => l.OccurredAt).HasDatabaseName("ix_ual_occurred_at");
    }
}

internal sealed class SyncInconsistencyConfiguration : IEntityTypeConfiguration<SyncInconsistency>
{
    public void Configure(EntityTypeBuilder<SyncInconsistency> b)
    {
        b.ToTable("sync_inconsistencies", "identity");

        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(s => s.Type).HasColumnName("type").HasMaxLength(40).IsRequired();
        b.Property(s => s.UserId).HasColumnName("user_id");
        b.Property(s => s.CognitoSub).HasColumnName("cognito_sub").HasMaxLength(64);
        b.Property(s => s.Email).HasColumnName("email").HasMaxLength(255);
        b.Property(s => s.Detail).HasColumnName("detail");
        b.Property(s => s.ResolvedAt).HasColumnName("resolved_at");
        b.Property(s => s.DetectedAt).HasColumnName("detected_at").IsRequired();

        b.ToTable(t => t.HasCheckConstraint(
            "chk_sync_type",
            "type IN ('COGNITO_ORPHAN','DB_ORPHAN','STATUS_DESYNCED')"));

        b.HasIndex(s => s.Type)
            .HasFilter("resolved_at IS NULL")
            .HasDatabaseName("ix_sync_type_unresolved");
    }
}
