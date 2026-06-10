using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.ProceduresConfig.Application;

public static class ListProcedureRulesCatalog
{
    public sealed record Query(Guid TenantId, Guid ProcedureTypeId);

    public sealed record ItemDto(
        Guid Id,
        string Name,
        string? Description,
        JsonElement ConditionTree,
        JsonElement Actions,
        int Priority,
        bool IsActive,
        int RowVersion);

    public static async Task<IReadOnlyList<ItemDto>> HandleAsync(
        Query query,
        IProcedureRulesCatalogRepository repo,
        CancellationToken ct = default)
    {
        var items = await repo.ListByProcedureTypeAsync(query.TenantId, query.ProcedureTypeId, ct);
        return items.Select(Map).ToList();
    }

    internal static ItemDto Map(ProcedureRuleCatalogRecord r) => new(
        r.Id,
        r.Name,
        r.Description,
        r.ConditionTree,
        r.Actions,
        r.Priority,
        r.IsActive,
        r.RowVersion);
}

public static class GetProcedureRuleCatalogEntry
{
    public sealed record Query(Guid TenantId, Guid Id);

    public static async Task<ListProcedureRulesCatalog.ItemDto?> HandleAsync(
        Query query,
        IProcedureRulesCatalogRepository repo,
        CancellationToken ct = default)
    {
        var item = await repo.GetByIdAsync(query.TenantId, query.Id, ct);
        return item is null ? null : ListProcedureRulesCatalog.Map(item);
    }
}

public static class CreateProcedureRuleCatalogEntry
{
    public sealed record Command(
        Guid TenantId,
        Guid ProcedureTypeId,
        string Name,
        string? Description,
        JsonElement ConditionTree,
        JsonElement Actions,
        int Priority,
        bool IsActive,
        Guid ActorUserId);

    public static async Task<Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>> HandleAsync(
        Command command,
        IProcedureRulesCatalogRepository repo,
        CancellationToken ct = default)
    {
        if (!TryValidateJson(command.ConditionTree, command.Actions, out var validationError))
        {
            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.Validation, validationError!));
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.Validation, "name es obligatorio."));
        }

        try
        {
            var created = await repo.AddAsync(
                new ProcedureRuleWriteModel(
                    Id: null,
                    TenantId: command.TenantId,
                    ProcedureTypeId: command.ProcedureTypeId,
                    Name: command.Name.Trim(),
                    Description: string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
                    ConditionTreeJson: command.ConditionTree.GetRawText(),
                    ActionsJson: command.Actions.GetRawText(),
                    Priority: command.Priority,
                    IsActive: command.IsActive,
                    ActorUserId: command.ActorUserId),
                ct);

            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Success(
                ListProcedureRulesCatalog.Map(created));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                                                    ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.Conflict, "Ya existe una regla con ese nombre."));
        }
    }

    internal static bool TryValidateJson(JsonElement conditionTree, JsonElement actions, out string? error)
    {
        if (!RuleJsonValidator.TryValidateCondition(conditionTree, out error))
        {
            return false;
        }

        return RuleJsonValidator.TryValidateActions(actions, out error);
    }
}

public static class UpdateProcedureRuleCatalogEntry
{
    public sealed record Command(
        Guid TenantId,
        Guid Id,
        string Name,
        string? Description,
        JsonElement ConditionTree,
        JsonElement Actions,
        int Priority,
        bool IsActive,
        int RowVersion,
        Guid ActorUserId);

    public static async Task<Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>> HandleAsync(
        Command command,
        IProcedureRulesCatalogRepository repo,
        CancellationToken ct = default)
    {
        if (!CreateProcedureRuleCatalogEntry.TryValidateJson(command.ConditionTree, command.Actions, out var validationError))
        {
            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.Validation, validationError!));
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.Validation, "name es obligatorio."));
        }

        var updated = await repo.UpdateAsync(
            new ProcedureRuleWriteModel(
                Id: command.Id,
                TenantId: command.TenantId,
                ProcedureTypeId: Guid.Empty,
                Name: command.Name.Trim(),
                Description: string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
                ConditionTreeJson: command.ConditionTree.GetRawText(),
                ActionsJson: command.Actions.GetRawText(),
                Priority: command.Priority,
                IsActive: command.IsActive,
                ActorUserId: command.ActorUserId,
                ExpectedRowVersion: command.RowVersion),
            ct);

        if (updated is null)
        {
            return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.NotFound, "Regla no encontrada."));
        }

        return Result<ListProcedureRulesCatalog.ItemDto, ProcedureRulesCatalogError>.Success(
            ListProcedureRulesCatalog.Map(updated));
    }
}

public static class DeleteProcedureRuleCatalogEntry
{
    public sealed record Command(Guid TenantId, Guid Id, Guid ActorUserId);

    public static async Task<Result<Unit, ProcedureRulesCatalogError>> HandleAsync(
        Command command,
        IProcedureRulesCatalogRepository repo,
        CancellationToken ct = default)
    {
        var deleted = await repo.SoftDeleteAsync(command.TenantId, command.Id, command.ActorUserId, ct);
        return deleted
            ? Result<Unit, ProcedureRulesCatalogError>.Success(default)
            : Result<Unit, ProcedureRulesCatalogError>.Failure(
                new ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind.NotFound, "Regla no encontrada."));
    }
}
