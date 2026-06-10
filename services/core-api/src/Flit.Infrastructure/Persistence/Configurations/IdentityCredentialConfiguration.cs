using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabla: identity.identity_credentials (ADR-0007).
/// PK = UserId (1:1 con Usuario). El hash se almacena cifrado en reposo
/// via Postgres Transparent Data Encryption o pgcrypto en cutover prod.
/// </summary>
internal sealed class IdentityCredentialConfiguration
    : IEntityTypeConfiguration<IdentityCredential>
{
    public void Configure(EntityTypeBuilder<IdentityCredential> builder)
    {
        builder.ToTable("identity_credentials", "identity");

        builder.HasKey(c => c.UserId);
        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(c => c.CambiadoEn)
            .HasColumnName("cambiado_en")
            .IsRequired();
    }
}
