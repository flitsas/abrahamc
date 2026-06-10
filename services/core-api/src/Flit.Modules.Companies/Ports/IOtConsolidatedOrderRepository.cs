using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

public sealed record OtConsolidatedOrderItemReadModel(
    Guid Id,
    int Position,
    string Source,
    Guid? ProcedureDocumentCatalogId,
    string? CatalogCode,
    string? CatalogName,
    string? CustomLabel);

public sealed record OtConsolidatedOrderReadModel(
    Guid OrderId,
    int Version,
    IReadOnlyList<OtConsolidatedOrderItemReadModel> Items);

public sealed record SaveConsolidatedOrderItemCommand(
    Guid? Id,
    int Position,
    string Source,
    Guid? ProcedureDocumentCatalogId,
    string? CustomLabel);

public interface IOtConsolidatedOrderRepository
{
    Task<OtConsolidatedDocOrder?> GetActiveOrderAsync(Guid trafficAgencyId, CancellationToken ct = default);

    Task<OtConsolidatedOrderReadModel?> GetActiveOrderReadModelAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default);

    Task AddOrderAsync(OtConsolidatedDocOrder order, CancellationToken ct = default);

    Task<bool> CatalogEntryExistsAsync(Guid catalogId, CancellationToken ct = default);

    Task ReplaceItemsAsync(
        Guid trafficAgencyId,
        Guid orderId,
        IReadOnlyList<SaveConsolidatedOrderItemCommand> items,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<bool> RemoveItemAsync(
        Guid trafficAgencyId,
        Guid orderId,
        Guid itemId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task TouchOrderAsync(
        Guid orderId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
