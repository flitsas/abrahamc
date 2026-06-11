using System.Text.RegularExpressions;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

internal static partial class CompanySlugHelper
{
    private static readonly Regex SlugRegex = new(
        "^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled);

    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.Compiled)]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRuns();

    public static bool IsValid(string slug) => SlugRegex.IsMatch(slug);

    public static string FromLegalName(string legalName)
    {
        var normalized = legalName.Trim().ToLowerInvariant();
        var slug = WhitespaceRuns().Replace(NonSlugChars().Replace(normalized, string.Empty), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "company" : slug;
    }

    public static async Task<string> ResolveUniqueAsync(
        string baseSlug,
        Func<string, CancellationToken, Task<bool>> slugExists,
        CancellationToken ct)
    {
        var candidate = baseSlug;
        var suffix = 2;
        while (await slugExists(candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}

public static class ListCompaniesIndex
{
    public sealed record Query(
        int Page,
        int Limit,
        Guid? CompanyId,
        string? Nit,
        string? Name,
        DateTimeOffset? CreatedFrom,
        DateTimeOffset? CreatedTo);

    public sealed record RowDto(
        Guid Id,
        Guid TenantId,
        string TenantName,
        string Nit,
        string LegalName,
        string? CommercialName,
        string Status,
        string ModulesEnabledJson,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        CompanyActionsDto Actions);

    public sealed record CompanyActionsDto(bool CanView, bool CanEdit);

    public sealed record Response(
        IReadOnlyList<RowDto> Data,
        int TotalCount,
        int Page,
        int PageSize);

    public static async Task<Response> HandleAsync(
        Query query,
        ICompaniesIndexRepository repo,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var limit = Math.Clamp(query.Limit, 1, 200);
        var listQuery = new CompanyIndexListQuery(
            page,
            limit,
            query.CompanyId,
            query.Nit,
            query.Name,
            query.CreatedFrom,
            query.CreatedTo);

        var (items, total) = await repo.ListAsync(listQuery, ct);
        var rows = items.Select(Map).ToList();
        return new Response(rows, total, page, limit);
    }

    internal static RowDto Map(CompanyIndexRow row) => new(
        row.Id,
        row.TenantId,
        row.TenantName,
        row.Nit,
        row.LegalName,
        row.CommercialName,
        row.Status,
        row.ModulesEnabledJson,
        row.CreatedAt,
        row.UpdatedAt,
        new CompanyActionsDto(CanView: true, CanEdit: true));
}

public static class GetCompanyIndex
{
    public sealed record Query(Guid Id);

    public sealed record Response(
        Guid Id,
        Guid TenantId,
        string TenantName,
        string Nit,
        string LegalName,
        string? CommercialName,
        string Status,
        string ModulesEnabledJson,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        ListCompaniesIndex.CompanyActionsDto Actions);

    public static async Task<Response?> HandleAsync(
        Query query,
        ICompaniesIndexRepository repo,
        CancellationToken ct = default)
    {
        var row = await repo.GetByIdAsync(query.Id, ct);
        return row is null
            ? null
            : new Response(
                row.Id,
                row.TenantId,
                row.TenantName,
                row.Nit,
                row.LegalName,
                row.CommercialName,
                row.Status,
                row.ModulesEnabledJson,
                row.CreatedAt,
                row.UpdatedAt,
                new ListCompaniesIndex.CompanyActionsDto(true, true));
    }
}

/// <summary>HU #9687 — alta maestro B2B (tenant + companies.companies + billetera firmas).</summary>
public static class CreateCompanyIndex
{
    public sealed record Command(
        string Nit,
        string LegalName,
        string? CommercialName,
        string? ContactEmail,
        string? Slug,
        string? ModulesEnabledJson,
        Guid ActorUserId,
        bool IsSuperAdmin);

    public sealed record Response(
        Guid Id,
        Guid TenantId,
        string Nit,
        string LegalName,
        string? CommercialName,
        string Status,
        string ModulesEnabledJson,
        DateTimeOffset CreatedAt);

    public static async Task<Result<Response, CompaniesError>> HandleAsync(
        Command cmd,
        ICompanyTenantProvisioner tenantProvisioner,
        ICompaniesRepository companiesRepo,
        Func<CancellationToken, Task<int>> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        if (!cmd.IsSuperAdmin)
        {
            return Result<Response, CompaniesError>.Failure(new CompaniesError(
                CompaniesErrorCode.Forbidden,
                "Solo SuperAdmin puede crear compañías maestro."));
        }

        if (string.IsNullOrWhiteSpace(cmd.Nit) || string.IsNullOrWhiteSpace(cmd.LegalName))
        {
            return Result<Response, CompaniesError>.Failure(new CompaniesError(
                CompaniesErrorCode.InvalidInput,
                "NIT y razón social son obligatorios."));
        }

        var nit = cmd.Nit.Trim();
        if (await tenantProvisioner.NitExistsAsync(nit, ct) ||
            await companiesRepo.ExistsByNitAsync(nit, ct))
        {
            return Result<Response, CompaniesError>.Failure(new CompaniesError(
                CompaniesErrorCode.NitConflict,
                "NIT ya registrado."));
        }

        var baseSlug = string.IsNullOrWhiteSpace(cmd.Slug)
            ? CompanySlugHelper.FromLegalName(cmd.LegalName)
            : cmd.Slug.Trim().ToLowerInvariant();

        if (!CompanySlugHelper.IsValid(baseSlug))
        {
            return Result<Response, CompaniesError>.Failure(new CompaniesError(
                CompaniesErrorCode.InvalidSlug,
                "Slug inválido. Use solo minúsculas, números y guiones."));
        }

        var slug = await CompanySlugHelper.ResolveUniqueAsync(
            baseSlug,
            tenantProvisioner.SlugExistsAsync,
            ct);

        var settings = BuildTenantSettings(cmd.ContactEmail);
        var tenant = await tenantProvisioner.CreateAsync(
            cmd.LegalName.Trim(),
            nit,
            slug,
            settings,
            cmd.ActorUserId,
            ct);

        var modulesJson = string.IsNullOrWhiteSpace(cmd.ModulesEnabledJson)
            ? "{}"
            : cmd.ModulesEnabledJson;

        var provision = await ProvisionCompanyProfile.ExecuteAsync(
            companiesRepo,
            saveChanges,
            new ProvisionCompanyProfile.Request(
                tenant.Id,
                nit,
                cmd.LegalName.Trim(),
                cmd.CommercialName,
                modulesJson,
                cmd.ActorUserId),
            clock.UtcNow,
            ct);

        if (!provision.IsSuccess)
        {
            return Result<Response, CompaniesError>.Failure(provision.Error);
        }

        return Result<Response, CompaniesError>.Success(new Response(
            provision.Value.CompanyId,
            tenant.Id,
            nit,
            cmd.LegalName.Trim(),
            string.IsNullOrWhiteSpace(cmd.CommercialName) ? null : cmd.CommercialName.Trim(),
            tenant.Status,
            modulesJson,
            clock.UtcNow));
    }

    private static string BuildTenantSettings(string? contactEmail)
    {
        if (string.IsNullOrWhiteSpace(contactEmail))
            return "{}";

        var escaped = contactEmail.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $$"""{"contactEmail":"{{escaped}}"}""";
    }
}

public enum UpdateCompanyIndexErrorCode
{
    NotFound,
}

public sealed record UpdateCompanyIndexError(UpdateCompanyIndexErrorCode Code, string Message);

public static class UpdateCompanyIndex
{
    public sealed record Command(
        Guid Id,
        string LegalName,
        string? CommercialName,
        string ModulesEnabledJson,
        Guid ActorUserId);

    public sealed record Response(Guid Id, DateTimeOffset UpdatedAt);

    public static async Task<Result<Response, UpdateCompanyIndexError>> HandleAsync(
        Command cmd,
        ICompaniesRepository companiesRepo,
        ICompaniesIndexRepository indexRepo,
        Func<CancellationToken, Task<int>> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var company = await companiesRepo.GetByIdAsync(cmd.Id, ct);
        if (company is null)
        {
            return Result<Response, UpdateCompanyIndexError>.Failure(
                new UpdateCompanyIndexError(UpdateCompanyIndexErrorCode.NotFound, "Compañía no encontrada."));
        }

        company.UpdateIndexProfile(
            cmd.LegalName,
            cmd.CommercialName,
            cmd.ModulesEnabledJson,
            cmd.ActorUserId,
            clock.UtcNow);

        await indexRepo.UpdateAsync(company, ct);
        await saveChanges(ct);

        return Result<Response, UpdateCompanyIndexError>.Success(
            new Response(company.Id, company.UpdatedAt));
    }
}
