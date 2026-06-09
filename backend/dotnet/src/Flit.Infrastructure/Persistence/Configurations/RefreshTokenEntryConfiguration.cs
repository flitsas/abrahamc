using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabla: identity.identity_refresh_tokens (ADR-0007).
/// Denylist de JTIs revocados. Un job de limpieza periodico elimina
/// filas expiradas (expires_at &lt; now()).
/// </summary>
internal sealed class RefreshTokenEntryConfiguration
    : IEntityTypeConfiguration<RefreshTokenEntry>
{
    public void Configure(EntityTypeBuilder<RefreshTokenEntry> builder)
    {
        builder.ToTable("identity_refresh_tokens", "identity");

        builder.HasKey(r => r.Jti);
        builder.Property(r => r.Jti)
            .HasColumnName("jti")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("reason")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.RevokedAt)
            .HasColumnName("revoked_at")
            .IsRequired();

        builder.HasIndex(r => r.ExpiresAt)
            .HasDatabaseName("ix_identity_refresh_tokens_expires_at");
    }
}
