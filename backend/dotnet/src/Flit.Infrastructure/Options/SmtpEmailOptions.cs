namespace Flit.Infrastructure.Options;

/// <summary>SMTP para correos transaccionales (onboarding HU #9418).</summary>
public sealed class SmtpEmailOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Servidor SMTP (ej. smtp.office365.com o localhost para MailHog).</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    /// <summary>Sin autenticación (MailHog DEV en puerto 4011).</summary>
    public bool DisableAuthentication { get; set; }

    public string? DefaultSenderEmail { get; set; }

    public string? DefaultSenderPassword { get; set; }

    public string DefaultSenderName { get; set; } = "FLIT Trámites";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(DefaultSenderEmail) &&
        (DisableAuthentication || !string.IsNullOrWhiteSpace(DefaultSenderPassword));
}
