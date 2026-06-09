namespace Flit.Modules.Companies.Application;

public enum CompaniesErrorCode
{
    TenantAlreadyHasCompany,
    TenantNotFound,
}

public sealed record CompaniesError(CompaniesErrorCode Code, string Message);
