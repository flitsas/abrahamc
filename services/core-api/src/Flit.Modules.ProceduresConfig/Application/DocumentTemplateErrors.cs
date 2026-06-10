namespace Flit.Modules.ProceduresConfig.Application;

public enum DocumentTemplateErrorKind
{
    Validation,
    NotFound,
    Conflict,
    ImmutableVersion,
}

public sealed record DocumentTemplateError(DocumentTemplateErrorKind Kind, string Message);
