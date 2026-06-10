namespace Flit.Modules.Integrations.Application;

public enum ExternalQueryErrorKind
{
    Validation,
    CircuitOpen,
    ProviderFailed,
}

public sealed record ExternalQueryError(ExternalQueryErrorKind Kind, string Message);
