namespace Flit.Modules.Identity.Domain;

/// <summary>
/// Consentimiento Habeas Data Ley 1581 Colombia.
/// Obligatorio para crear cualquier Usuario (ciudadano o funcionario).
/// ADR-0006 §"Cumplimiento normativo" Art. 4.
/// </summary>
public sealed record HabeasDataConsent(
    bool ConsentimientoOtorgado,
    DateTimeOffset FechaConsentimiento,
    string PoliticaVersion);
