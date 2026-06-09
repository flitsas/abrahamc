namespace Flit.Modules.ProceduresConfig.Domain;

public enum ProcedureActorErrorKind
{
    TypeNotFound,
    EdgeNotInMatrix,
    UnknownDocumentType,
    InvalidVehicleIdentification,
    MandatoryQueryOmissionForbidden,
}

public sealed record ProcedureActorError(ProcedureActorErrorKind Kind, string Message)
{
    public static ProcedureActorError EdgeNotInMatrix(string edgeCode) =>
        new(ProcedureActorErrorKind.EdgeNotInMatrix, $"La arista '{edgeCode}' no está en la matriz o está desactivada.");

    public static ProcedureActorError UnknownDocumentType(string code) =>
        new(ProcedureActorErrorKind.UnknownDocumentType, $"Tipo de documento '{code}' no reconocido.");

    public static ProcedureActorError InvalidVehicle(string detail) =>
        new(ProcedureActorErrorKind.InvalidVehicleIdentification, detail);
}

public sealed record QueryExecutionPlan(
    string ConnectorCode,
    string? EdgeCode,
    bool IsMandatory,
    bool IsOmitible,
    string PersonKindFilter);

public sealed record ActorIncorporationResult(
    string PersonKind,
    IReadOnlyList<QueryExecutionPlan> QueriesToExecute,
    IReadOnlyList<string> QueriesSkipped);
