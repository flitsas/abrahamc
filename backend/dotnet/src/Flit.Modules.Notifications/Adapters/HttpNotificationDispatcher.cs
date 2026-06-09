using System.Text;
using Flit.Modules.Notifications.Ports;

namespace Flit.Modules.Notifications.Adapters;

/// <summary>
/// LEGACY (post ADR-0014): despachaba la notificacion via HTTP POST al
/// endpoint internal /internal/notifications/send de node-bff cuando este
/// existia. Reemplazo: RabbitMQ consumer + SignalR Hub
/// (Flit.Modules.Notifications.Adapters.SignalR.FlitNotificationsHub).
///
/// Mantenido en compat mode hasta que la US de migracion completa el cableado
/// a RabbitMQ. Solo se registra si NodeBff:BaseUrl explicito en Program.cs.
/// Best-effort: no bloquea la operacion principal si falla.
/// </summary>
[Obsolete("Migrar a RabbitMQ consumer + SignalR Hub (ADR-0014).")]
public sealed class HttpNotificationDispatcher : INotificationDispatcher
{
    private readonly HttpClient _http;
    private readonly string _internalToken;

    public HttpNotificationDispatcher(HttpClient http, string internalToken)
    {
        _http = http;
        _internalToken = internalToken;
    }

    public async Task DispatchAsync(NotificationDispatchEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Convertimos el canal del dominio (CanalNotificacion enum, espanol)
        // al contrato legacy (EMAIL/SMS/PUSH en ingles).
        var channelStr = evt.Channel.ToString().ToUpperInvariant() switch
        {
            "EMAIL" => "EMAIL",
            "SMS" => "SMS",
            "PUSH" => "PUSH",
            _ => "EMAIL", // fallback
        };

        // JSON manual para evitar JsonSerializer.Serialize<T> (AOT-friendly).
        var sb = new StringBuilder(512);
        sb.Append("{\"deliveryId\":\"").Append(evt.DeliveryId).Append("\",")
          .Append("\"channel\":\"").Append(channelStr).Append("\",")
          .Append("\"recipient\":").Append(JsonString(evt.Recipient)).Append(',');
        if (evt.Subject is null)
            sb.Append("\"subject\":null,");
        else
            sb.Append("\"subject\":").Append(JsonString(evt.Subject)).Append(',');
        sb.Append("\"body\":").Append(JsonString(evt.Body)).Append(',');
        sb.Append("\"metadata\":{");
        var first = true;
        foreach (var kv in evt.Metadata)
        {
            if (!first) sb.Append(',');
            sb.Append(JsonString(kv.Key)).Append(':').Append(JsonString(kv.Value));
            first = false;
        }
        sb.Append("}}");

        using var req = new HttpRequestMessage(HttpMethod.Post, "internal/notifications/send")
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Internal-Token", _internalToken);

        using var res = await _http.SendAsync(req, ct);
        // 2xx = ok. El endpoint legacy responde 202 al aceptar el job.
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Escapado JSON minimo (comillas, backslash, control chars basicos).
    /// Suficiente para email/subject/body sin caracteres raros. En produccion
    /// con cuerpos HTML complejos considerar JsonSerializer + SourceGen.
    /// </summary>
    private static string JsonString(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append($"\\u{(int)c:x4}");
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
