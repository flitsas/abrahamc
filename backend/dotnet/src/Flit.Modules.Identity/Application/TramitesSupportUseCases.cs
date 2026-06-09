using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Tickets de soporte (HU #9420/#9421 prereq).</summary>
public static class TramitesSupportUseCases
{
    public const string TicketNotFoundCode = "TICKET_NOT_FOUND";
    public const string InvalidStatusCode = "INVALID_STATUS";
    public const string InvalidSubjectCode = "INVALID_SUBJECT";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "open", "in_progress", "resolved", "closed",
    };

    public sealed record CreateTicketCommand(string Subject, string Body, string? Category);

    public sealed record UpdateTicketCommand(string? Status, Guid? AssignedToUserId);

    public static async Task<Result<SupportTicketRow, string>> CreateTicketAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CreateTicketCommand cmd,
        IIdentitySupportRepository support,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Subject))
            return Result<SupportTicketRow, string>.Failure(InvalidSubjectCode);

        await support.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);
        var row = await support.CreateTicketAsync(
            tenantId,
            userId,
            cmd.Subject.Trim(),
            cmd.Body.Trim(),
            string.IsNullOrWhiteSpace(cmd.Category) ? "general" : cmd.Category.Trim(),
            userId,
            ct);

        return Result<SupportTicketRow, string>.Success(row);
    }

    public static async Task<Result<(IReadOnlyList<SupportTicketRow> Items, int Total), string>>
        ListTicketsAsync(
            Guid tenantId,
            Guid userId,
            bool isSuperAdmin,
            bool adminView,
            bool tenantIdProvidedInQuery,
            Guid? reporterUserId,
            string? status,
            int page,
            int limit,
            IIdentitySupportRepository support,
            CancellationToken ct = default)
    {
        await support.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);

        // Perfil: sin tenantId en query → solo tickets del usuario actual.
        // Consola admin: con tenantId en query y vista admin → todos los del tenant.
        Guid? filterReporter = adminView && tenantIdProvidedInQuery && !reporterUserId.HasValue
            ? null
            : reporterUserId ?? userId;
        var list = await support.ListTicketsAsync(
            tenantId, filterReporter, status, page, limit, ct);

        return Result<(IReadOnlyList<SupportTicketRow>, int), string>.Success(list);
    }

    public static async Task<Result<SupportTicketRow, string>> GetTicketAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        bool adminView,
        Guid ticketId,
        IIdentitySupportRepository support,
        CancellationToken ct = default)
    {
        await support.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);
        var row = await support.GetTicketAsync(tenantId, ticketId, ct);
        if (row is null)
            return Result<SupportTicketRow, string>.Failure(TicketNotFoundCode);

        if (!adminView && row.ReporterUserId != userId)
            return Result<SupportTicketRow, string>.Failure(TicketNotFoundCode);

        return Result<SupportTicketRow, string>.Success(row);
    }

    public static async Task<Result<SupportTicketRow, string>> UpdateTicketAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        Guid ticketId,
        UpdateTicketCommand cmd,
        IIdentitySupportRepository support,
        CancellationToken ct = default)
    {
        if (cmd.Status is not null && !AllowedStatuses.Contains(cmd.Status))
            return Result<SupportTicketRow, string>.Failure(InvalidStatusCode);

        await support.ApplySessionGucAsync(tenantId, userId, isSuperAdmin, ct);
        var updated = await support.UpdateTicketAsync(
            tenantId, ticketId, cmd.Status, cmd.AssignedToUserId, userId, ct);
        if (!updated)
            return Result<SupportTicketRow, string>.Failure(TicketNotFoundCode);

        var row = await support.GetTicketAsync(tenantId, ticketId, ct);
        return row is null
            ? Result<SupportTicketRow, string>.Failure(TicketNotFoundCode)
            : Result<SupportTicketRow, string>.Success(row);
    }
}
