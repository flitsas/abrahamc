namespace Flit.Modules.ProceduresConfig.Application;

public enum ProcedureRulesCatalogErrorKind
{
    NotFound,
    Conflict,
    Validation,
}

public sealed record ProcedureRulesCatalogError(ProcedureRulesCatalogErrorKind Kind, string Message);
