using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Flit.Modules.Notifications.Domain;

namespace Flit.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabla: notifications.notification_delivery (ADR-0007, FLIT 2.0).
/// Append-only: REVOKE UPDATE/DELETE post-cutover prod.
/// NotificationDelivery es un record (inmutable) — EF Core mapea via
/// constructor positional con columnas coincidentes.
///
/// El dominio Y la ejecucion de Notifications viven en core-api (.NET)
/// post ADR-0014. El consumer RabbitMQ in-process (Flit.Modules.Notifications)
/// ejecuta el envio externo (SMTP/SMS/push).
///
/// Nota: las column names internas conservan los nombres en espanol del MVP
/// (tipo, canal, destinatario_*) hasta el rewrite completo del modulo
/// Notifications en Fase 7. El nombre de TABLA y SCHEMA si estan en ingles.
/// </summary>
internal sealed class NotificationDeliveryConfiguration
    : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_delivery", "notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(n => n.Tipo)
            .HasColumnName("notification_type")
            .HasMaxLength(40)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(n => n.Canal)
            .HasColumnName("channel")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(n => n.DestinatarioUserId)
            .HasColumnName("recipient_user_id")
            .HasColumnType("uuid");

        builder.Property(n => n.DestinatarioContacto)
            .HasColumnName("recipient_masked")
            .HasMaxLength(255);

        builder.Property(n => n.TramiteId)
            .HasColumnName("procedure_id")
            .HasColumnType("uuid");

        builder.Property(n => n.Estado)
            .HasColumnName("status")
            .HasMaxLength(15)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(n => n.Asunto)
            .HasColumnName("subject")
            .HasMaxLength(300);

        builder.Property(n => n.CuerpoResumen)
            .HasColumnName("body_excerpt")
            .HasMaxLength(200);

        builder.Property(n => n.ErrorMensaje)
            .HasColumnName("error_message")
            .HasMaxLength(1000);

        builder.Property(n => n.CreadoEn)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(n => n.EnviadoEn)
            .HasColumnName("sent_at");

        builder.HasIndex(n => n.DestinatarioUserId)
            .HasDatabaseName("ix_notification_delivery_recipient_user_id");
        builder.HasIndex(n => n.TramiteId)
            .HasDatabaseName("ix_notification_delivery_procedure_id");
        builder.HasIndex(n => n.CreadoEn)
            .HasDatabaseName("ix_notification_delivery_created_at");
    }
}
