namespace Flit.Modules.ProceduresConfig.Application;

public enum EndpointCatalogErrorKind
{
    NotFound,
    Conflict,
    Validation,
    RateLimited,
}

public sealed record EndpointCatalogError(EndpointCatalogErrorKind Kind, string Message);
