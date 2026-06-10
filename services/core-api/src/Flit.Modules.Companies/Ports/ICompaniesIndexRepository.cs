namespace Flit.Modules.Companies.Ports;

public sealed record CompanyIndexRow(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string Nit,
    string LegalName,
    string? CommercialName,
    string ModulesEnabledJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CompanyIndexListQuery(
    int Page,
    int Limit,
    Guid? CompanyId,
    string? Nit,
    string? Name,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo);

public interface ICompaniesIndexRepository
{
    Task<(IReadOnlyList<CompanyIndexRow> Items, int Total)> ListAsync(
        CompanyIndexListQuery query,
        CancellationToken ct = default);

    Task<CompanyIndexRow?> GetByIdAsync(Guid companyId, CancellationToken ct = default);

    Task UpdateAsync(
        Domain.Company company,
        CancellationToken ct = default);
}
