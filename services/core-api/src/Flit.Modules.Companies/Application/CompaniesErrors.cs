namespace Flit.Modules.Companies.Application;

public enum CompaniesErrorCode
{
    TenantAlreadyHasCompany,
    TenantNotFound,
    NitConflict,
    SlugConflict,
    InvalidSlug,
    Forbidden,
    InvalidInput,
}

public sealed record CompaniesError(CompaniesErrorCode Code, string Message);
