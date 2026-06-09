namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Plantilla SMTP HTML por tenant/tipo de tramite.
/// Tabla: identity_verification.verification_email_templates.
/// </summary>
public sealed class VerificationEmailTemplate
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string TemplateKey { get; private set; } = string.Empty;
    public Guid? ProcedureTypeId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string HtmlBody { get; private set; } = string.Empty;
    public string Locale { get; private set; } = "es-CO";
    public bool IsActive { get; private set; }

    private VerificationEmailTemplate() { }

    public static VerificationEmailTemplate CreateDefault(string templateKey = "default") =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = null,
            TemplateKey = templateKey,
            ProcedureTypeId = null,
            Subject = "Validación de identidad — FLIT IDSecure",
            HtmlBody = """
                <html><body>
                <p>Hola,</p>
                <p>Debe completar la validación de identidad para su trámite.</p>
                <p><a href="{{validationUrl}}">Iniciar validación</a></p>
                <p>Este enlace es de un solo uso y expira el {{expiresAt}} (48 horas).</p>
                </body></html>
                """,
            Locale = "es-CO",
            IsActive = true,
        };
}
