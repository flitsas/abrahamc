using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

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
        string ModulesEnabledJson,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        CompanyActionsDto Actions);

    public sealed record CompanyActionsDto(bool CanView, bool CanEdit);

    public sealed record Response(
        IReadOnlyList<RowDto> Data,
        int Total,
        int Page,
        int Limit);

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
                row.ModulesEnabledJson,
                row.CreatedAt,
                row.UpdatedAt,
                new ListCompaniesIndex.CompanyActionsDto(true, true));
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
