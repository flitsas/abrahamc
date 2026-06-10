namespace Flit.Modules.Identity.Domain;

/// <summary>Ticket de soporte con contexto del colaborador (RF-4.4).</summary>
public sealed record SupportTicketRow(
    Guid Id,
    Guid TenantId,
    string? TenantName,
    Guid ReporterUserId,
    string ReporterEmail,
    string? ReporterFullName,
    Guid? AssignedToUserId,
    string Subject,
    string Body,
    string Category,
    string Status,
    DateTimeOffset CreatedAt,
    int RowVersion);
