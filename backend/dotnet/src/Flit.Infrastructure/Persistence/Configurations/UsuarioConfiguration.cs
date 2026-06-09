using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Flit.Modules.Identity.Domain;

namespace Flit.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API config para Usuario (aggregate root Identity).
/// Tabla: identity.identity_users (ADR-0007).
///
/// Value Objects Email, Documento y HabeasDataConsent se mapean como
/// columnas aplanadas (owned entity o columnas simples) porque el dominio
/// usa constructores privados — EF Core accede vía reflexion en JIT.
/// </summary>
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("identity_users", "identity");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        // Email Value Object → columna email
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired();
            // Indice sobre la columna email del owned entity (nombre de propiedad C#)
            email.HasIndex(e => e.Value).IsUnique().HasDatabaseName("ix_identity_users_email");
        });

        // Documento Value Object → columnas documento_tipo + documento_numero
        builder.OwnsOne(u => u.Documento, doc =>
        {
            doc.Property(d => d.Tipo)
                .HasColumnName("documento_tipo")
                .HasMaxLength(10)
                .HasConversion<string>()
                .IsRequired();
            doc.Property(d => d.Numero)
                .HasColumnName("documento_numero")
                .HasMaxLength(20)
                .IsRequired();
            doc.HasIndex(d => new { d.Tipo, d.Numero })
                .IsUnique()
                .HasDatabaseName("ix_identity_users_documento");
        });

        builder.Property(u => u.Nombres)
            .HasColumnName("nombres")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Apellidos)
            .HasColumnName("apellidos")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.FechaNacimiento)
            .HasColumnName("fecha_nacimiento");

        builder.Property(u => u.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(20);

        builder.Property(u => u.Rol)
            .HasColumnName("rol")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(u => u.OrganismoId)
            .HasColumnName("organismo_id")
            .HasColumnType("uuid");

        builder.Property(u => u.Activo)
            .HasColumnName("activo")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.Bloqueado)
            .HasColumnName("bloqueado")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.BloqueadoHasta)
            .HasColumnName("bloqueado_hasta");

        builder.Property(u => u.IntentosFallidos)
            .HasColumnName("intentos_fallidos")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.UltimoLogin)
            .HasColumnName("ultimo_login");

        builder.Property(u => u.EmailVerificado)
            .HasColumnName("email_verificado")
            .IsRequired()
            .HasDefaultValue(false);

        // HabeasDataConsent Value Object → columnas aplanadas
        builder.OwnsOne(u => u.Consent, consent =>
        {
            consent.Property(c => c.ConsentimientoOtorgado)
                .HasColumnName("habeas_data_consentimiento")
                .IsRequired()
                .HasDefaultValue(false);
            consent.Property(c => c.FechaConsentimiento)
                .HasColumnName("habeas_data_fecha");
            consent.Property(c => c.PoliticaVersion)
                .HasColumnName("habeas_data_politica_version")
                .HasMaxLength(20);
        });

        builder.Property(u => u.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.Property(u => u.ActualizadoEn)
            .HasColumnName("actualizado_en")
            .IsRequired();

        builder.HasIndex(u => u.Activo)
            .HasDatabaseName("ix_identity_users_activo");
    }
}
