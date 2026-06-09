using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9427 — validación de arista + enrutamiento CC/NIT → consultas.</summary>
public static class IncorporateProcedureActors
{
    public sealed record Command(
        Guid TenantId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        string DocumentTypeCode,
        IReadOnlyDictionary<string, string?>? CapturedFields);

    public static async Task<(ActorIncorporationResult? Ok, ProcedureActorError? Error)> HandleAsync(
        Command command,
        IProceduresConfigReadRepository configRepo,
        IDocumentTypesReadRepository documentTypesRepo,
        CancellationToken ct = default)
    {
        var config = await GetProcedureConfiguration.HandleAsync(
            new GetProcedureConfiguration.Query(command.TenantId, command.ProcedureTypeCode, command.TrafficAgencyId),
            configRepo,
            ct);

        if (config is null)
        {
            return (null, new ProcedureActorError(ProcedureActorErrorKind.TypeNotFound, "Tipo de trámite no encontrado o inactivo."));
        }

        var edge = config.Edges.FirstOrDefault(e =>
            string.Equals(e.Code, command.EdgeCode, StringComparison.OrdinalIgnoreCase));

        if (edge is null || !edge.IsActive)
        {
            return (null, ProcedureActorError.EdgeNotInMatrix(command.EdgeCode));
        }

        if (string.Equals(command.EdgeCode, "vehiculo", StringComparison.OrdinalIgnoreCase) &&
            command.CapturedFields is not null)
        {
            var vehicleError = VehicleIdentificationValidator.ValidateVehicleFields(command.CapturedFields);
            if (vehicleError is not null)
            {
                return (null, vehicleError);
            }
        }

        var docType = await documentTypesRepo.GetByCodeAsync(command.DocumentTypeCode, ct);
        if (docType is null || string.IsNullOrWhiteSpace(docType.DefaultPersonKind))
        {
            return (null, ProcedureActorError.UnknownDocumentType(command.DocumentTypeCode));
        }

        var personKind = docType.DefaultPersonKind;
        var edgeQueries = config.Queries
            .Where(q => string.Equals(q.EdgeCode ?? string.Empty, command.EdgeCode, StringComparison.OrdinalIgnoreCase)
                        || q.EdgeCode is null)
            .ToList();

        var toExecute = new List<QueryExecutionPlan>();
        var skipped = new List<string>();

        foreach (var q in edgeQueries.OrderBy(x => x.DisplayOrder))
        {
            var matches = q.PersonKindFilter is "any"
                || string.Equals(q.PersonKindFilter, personKind, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                toExecute.Add(new QueryExecutionPlan(
                    q.ConnectorCode,
                    q.EdgeCode,
                    q.IsMandatory,
                    q.IsOmitible,
                    q.PersonKindFilter));
            }
            else
            {
                skipped.Add(q.ConnectorCode);
            }
        }

        toExecute = LeasingLocatarioRules.Enforce(command.ProcedureTypeCode, command.EdgeCode, toExecute);

        return (new ActorIncorporationResult(personKind, toExecute, skipped), null);
    }
}
