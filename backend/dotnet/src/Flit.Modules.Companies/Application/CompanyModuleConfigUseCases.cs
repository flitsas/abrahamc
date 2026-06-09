using System.Text.Json;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

public static class ListCompanyModuleConfigs
{
    public sealed record Query(Guid TenantId);

    public sealed record ConfigDto(
        Guid Id,
        string ModuleKey,
        string ConfigJson,
        bool IsActive,
        int Version,
        DateTimeOffset UpdatedAt);

    public sealed record Response(IReadOnlyList<ConfigDto> Items);

    public static async Task<Response> HandleAsync(
        Query query,
        ICompanyModuleConfigsRepository repo,
        CancellationToken ct = default)
    {
        var items = await repo.ListByTenantAsync(query.TenantId, ct);
        return new Response(items.Select(Map).ToList());
    }

    internal static ConfigDto Map(CompanyModuleConfig c) => new(
        c.Id, c.ModuleKey, c.ConfigJson, c.IsActive, c.Version, c.UpdatedAt);
}

public static class GetCompanyModuleConfig
{
    public sealed record Query(Guid TenantId, string ModuleKey);

    public static async Task<ListCompanyModuleConfigs.ConfigDto?> HandleAsync(
        Query query,
        ICompanyModuleConfigsRepository repo,
        CancellationToken ct = default)
    {
        var row = await repo.GetByTenantAndModuleAsync(query.TenantId, query.ModuleKey, ct);
        return row is null ? null : ListCompanyModuleConfigs.Map(row);
    }
}

public enum UpsertModuleConfigErrorCode
{
    InvalidModuleKey,
    InvalidConfigJson,
}

public sealed record UpsertModuleConfigError(UpsertModuleConfigErrorCode Code, string Message);

public static class UpsertCompanyModuleConfig
{
    public sealed record Command(
        Guid TenantId,
        string ModuleKey,
        string ConfigJson,
        bool IsActive,
        Guid ActorUserId);

    public sealed record Response(
        string ModuleKey,
        string ConfigJson,
        int Version,
        DateTimeOffset UpdatedAt,
        bool HotReloadApplied);

    public static async Task<Result<Response, UpsertModuleConfigError>> HandleAsync(
        Command cmd,
        ICompanyModuleConfigsRepository repo,
        Func<CancellationToken, Task<int>> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        if (!CompanyModuleKey.All.Contains(cmd.ModuleKey))
        {
            return Result<Response, UpsertModuleConfigError>.Failure(
                new UpsertModuleConfigError(
                    UpsertModuleConfigErrorCode.InvalidModuleKey,
                    $"module_key inválido: '{cmd.ModuleKey}'. Valores: registration, transfers, company, runt_contingency."));
        }

        if (!IsValidJson(cmd.ConfigJson))
        {
            return Result<Response, UpsertModuleConfigError>.Failure(
                new UpsertModuleConfigError(
                    UpsertModuleConfigErrorCode.InvalidConfigJson,
                    "config debe ser JSON válido."));
        }

        var now = clock.UtcNow;
        var existing = await repo.GetByTenantAndModuleAsync(cmd.TenantId, cmd.ModuleKey, ct);
        if (existing is null)
        {
            var created = CompanyModuleConfig.Create(
                cmd.TenantId,
                cmd.ModuleKey,
                cmd.ConfigJson,
                cmd.ActorUserId,
                now,
                cmd.IsActive);

            await repo.AddAsync(created, ct);
            await saveChanges(ct);
            return Result<Response, UpsertModuleConfigError>.Success(
                new Response(cmd.ModuleKey, created.ConfigJson, created.Version, created.UpdatedAt, true));
        }

        existing.ApplyHotReload(cmd.ConfigJson, cmd.IsActive, cmd.ActorUserId, now);
        repo.Update(existing);
        await saveChanges(ct);

        return Result<Response, UpsertModuleConfigError>.Success(
            new Response(cmd.ModuleKey, existing.ConfigJson, existing.Version, existing.UpdatedAt, true));
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public static class GetSignatureWallet
{
    public sealed record Query(Guid TenantId);

    public sealed record Response(
        Guid Id,
        int Balance,
        int LowThreshold,
        bool AutoRecharge,
        DateTimeOffset UpdatedAt);

    public static async Task<Response?> HandleAsync(
        Query query,
        ICompanyModuleConfigsRepository repo,
        CancellationToken ct = default)
    {
        var wallet = await repo.GetSignatureWalletByTenantAsync(query.TenantId, ct);
        return wallet is null
            ? null
            : new Response(wallet.Id, wallet.Balance, wallet.LowThreshold, wallet.AutoRecharge, wallet.UpdatedAt);
    }
}
