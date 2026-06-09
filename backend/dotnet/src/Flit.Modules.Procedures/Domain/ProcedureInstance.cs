namespace Flit.Modules.Procedures.Domain;

/// <summary>
/// Instancia radicada de un trámite. Espejo directo de <c>procedures.procedure_instances</c>.
/// <see cref="ConfigSnapshot"/> es inmutable desde la radicación (ADR-0010): el runtime opera
/// sobre la configuración capturada en el momento de la radicación, independiente de ediciones
/// posteriores a los parámetros globales o del tenant.
/// </summary>
public sealed record ProcedureInstance(
    Guid Id,
    Guid TenantId,
    Guid ProcedureTypeId,
    Guid? TrafficAgencyId,
    string ReferenceNumber,
    string State,
    string ConfigSnapshot,
    int ConfigSchemaVersion,
    Guid? AssignedToUserId,
    DateTimeOffset? RadicatedAt,
    decimal TotalAmount,
    string CurrencyCode,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    int RowVersion);
