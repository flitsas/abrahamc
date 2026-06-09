using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>Tickets de soporte (RF-4.4, prereq HU #9420/#9421).</summary>
public interface IIdentitySupportRepository
{
    Task ApplySessionGucAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default);

    Task<SupportTicketRow> CreateTicketAsync(
        Guid tenantId,
        Guid reporterUserId,
        string subject,
        string body,
        string category,
        Guid createdBy,
        CancellationToken ct = default);

    Task<(IReadOnlyList<SupportTicketRow> Items, int Total)> ListTicketsAsync(
        Guid tenantId,
        Guid? reporterUserId,
        string? status,
        int page,
        int limit,
        CancellationToken ct = default);

    Task<SupportTicketRow?> GetTicketAsync(
        Guid tenantId,
        Guid ticketId,
        CancellationToken ct = default);

    Task<bool> UpdateTicketAsync(
        Guid tenantId,
        Guid ticketId,
        string? status,
        Guid? assignedToUserId,
        Guid updatedBy,
        CancellationToken ct = default);
}
