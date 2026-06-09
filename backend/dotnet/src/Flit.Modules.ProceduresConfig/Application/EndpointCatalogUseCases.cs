using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.ProceduresConfig.Application;

public static class ListEndpointCatalog
{
    public sealed record Query(Guid TenantId);

    public sealed record ItemDto(
        Guid Id,
        string Code,
        string Name,
        string Url,
        string Method,
        string AuthType,
        JsonElement AuthConfig,
        int TimeoutMs,
        bool IsActive,
        int RowVersion);

    public static async Task<IReadOnlyList<ItemDto>> HandleAsync(
        Query query,
        IEndpointCatalogRepository repo,
        CancellationToken ct = default)
    {
        var items = await repo.ListByTenantAsync(query.TenantId, ct);
        return items.Select(Map).ToList();
    }

    internal static ItemDto Map(EndpointCatalogRecord r) => new(
        r.Id,
        r.Code,
        r.Name,
        r.Url,
        r.Method,
        r.AuthType,
        EndpointAuthConfigValidator.SanitizeForResponse(r.AuthConfig),
        r.TimeoutMs,
        r.IsActive,
        r.RowVersion);
}

public static class GetEndpointCatalogEntry
{
    public sealed record Query(Guid TenantId, Guid Id);

    public static async Task<ListEndpointCatalog.ItemDto?> HandleAsync(
        Query query,
        IEndpointCatalogRepository repo,
        CancellationToken ct = default)
    {
        var item = await repo.GetByIdAsync(query.TenantId, query.Id, ct);
        return item is null ? null : ListEndpointCatalog.Map(item);
    }
}

public static class CreateEndpointCatalogEntry
{
    public sealed record Command(
        Guid TenantId,
        string Code,
        string Name,
        string Url,
        string Method,
        string AuthType,
        JsonElement AuthConfig,
        int TimeoutMs,
        bool IsActive,
        Guid ActorUserId);

    public static async Task<Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>> HandleAsync(
        Command command,
        IEndpointCatalogRepository repo,
        CancellationToken ct = default)
    {
        if (!EndpointAuthConfigValidator.TryValidate(command.AuthType, command.AuthConfig, out var authError))
        {
            return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.Validation, authError!));
        }

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name) ||
            string.IsNullOrWhiteSpace(command.Url))
        {
            return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.Validation, "code, name y url son obligatorios."));
        }

        try
        {
            var created = await repo.AddAsync(
                new EndpointCatalogWriteModel(
                    Id: null,
                    TenantId: command.TenantId,
                    Code: command.Code.Trim(),
                    Name: command.Name.Trim(),
                    Url: command.Url.Trim(),
                    Method: command.Method.Trim().ToUpperInvariant(),
                    AuthType: command.AuthType.Trim().ToLowerInvariant(),
                    AuthConfigJson: command.AuthConfig.GetRawText(),
                    TimeoutMs: command.TimeoutMs,
                    IsActive: command.IsActive,
                    ActorUserId: command.ActorUserId),
                ct);

            return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Success(
                ListEndpointCatalog.Map(created));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                                                    ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.Conflict, "Ya existe un endpoint con ese código."));
        }
    }
}

public static class UpdateEndpointCatalogEntry
{
    public sealed record Command(
        Guid TenantId,
        Guid Id,
        string Name,
        string Url,
        string Method,
        string AuthType,
        JsonElement AuthConfig,
        int TimeoutMs,
        bool IsActive,
        int RowVersion,
        Guid ActorUserId);

    public static async Task<Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>> HandleAsync(
        Command command,
        IEndpointCatalogRepository repo,
        CancellationToken ct = default)
    {
        if (!EndpointAuthConfigValidator.TryValidate(command.AuthType, command.AuthConfig, out var authError))
        {
            return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.Validation, authError!));
        }

        var updated = await repo.UpdateAsync(
            new EndpointCatalogWriteModel(
                Id: command.Id,
                TenantId: command.TenantId,
                Code: string.Empty,
                Name: command.Name.Trim(),
                Url: command.Url.Trim(),
                Method: command.Method.Trim().ToUpperInvariant(),
                AuthType: command.AuthType.Trim().ToLowerInvariant(),
                AuthConfigJson: command.AuthConfig.GetRawText(),
                TimeoutMs: command.TimeoutMs,
                IsActive: command.IsActive,
                ActorUserId: command.ActorUserId,
                ExpectedRowVersion: command.RowVersion),
            ct);

        if (updated is null)
        {
            return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.NotFound, "Endpoint no encontrado."));
        }

        return Result<ListEndpointCatalog.ItemDto, EndpointCatalogError>.Success(
            ListEndpointCatalog.Map(updated));
    }
}

public static class DeleteEndpointCatalogEntry
{
    public sealed record Command(Guid TenantId, Guid Id, Guid ActorUserId);

    public static async Task<Result<Unit, EndpointCatalogError>> HandleAsync(
        Command command,
        IEndpointCatalogRepository repo,
        CancellationToken ct = default)
    {
        var deleted = await repo.SoftDeleteAsync(command.TenantId, command.Id, command.ActorUserId, ct);
        return deleted
            ? Result<Unit, EndpointCatalogError>.Success(default)
            : Result<Unit, EndpointCatalogError>.Failure(
                new EndpointCatalogError(EndpointCatalogErrorKind.NotFound, "Endpoint no encontrado."));
    }
}

public readonly record struct Unit;
