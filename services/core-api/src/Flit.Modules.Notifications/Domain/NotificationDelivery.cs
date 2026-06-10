namespace Flit.Modules.Notifications.Domain;

/// <summary>
/// Canal por el que se envia (o intenta) la notificacion.
/// MVP: SMS diferido por decision LT 2026-05-21. Push opcional.
/// </summary>
public enum CanalNotificacion
{
    Email,
    Sms,
    Push,
    WebSocket,
}

/// <summary>
/// Tipo de evento que dispara la notificacion. Se mapea 1:1 con eventos
/// de dominio Procedures (flit.procedures/*).
/// </summary>
public enum TipoNotificacion
{
    TramiteCreado,
    TramiteRadicado,
    TramiteEnRevision,
    TramiteAprobado,
    TramiteRechazado,
    UsuarioRegistrado,
    PasswordReset,
}

/// <summary>
/// Outcome del intento de envio. Persistido inmutable para audit
/// (Habeas Data + SOC2 CC7.2 monitoreo).
/// </summary>
public enum EstadoEnvio
{
    Pendiente,
    Enviado,
    Fallido,
    Diferido,   // canal configurado para diferirse (e.g. SMS sin proveedor)
}

/// <summary>
/// Audit log de cada intento de notificacion. Tabla
/// core.notificaciones_log (ADR-0007 prefix).
///
/// Append-only en Postgres (REVOKE UPDATE, DELETE post-cutover).
/// </summary>
public sealed record NotificationDelivery(
    Guid Id,
    TipoNotificacion Tipo,
    CanalNotificacion Canal,
    Guid? DestinatarioUserId,
    string? DestinatarioContacto, // email, telefono o deviceToken hasheado
    Guid? TramiteId,
    EstadoEnvio Estado,
    string? Asunto,
    string? CuerpoResumen,        // primeras 200 chars (sin PII bruta)
    string? ErrorMensaje,
    DateTimeOffset CreadoEn,
    DateTimeOffset? EnviadoEn);
