using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU #10081 — validación de copropiedad 100% para rol propietario.</summary>
public static class ValidateProcedureOwnership
{
    public sealed record Query(Guid ProcedureInstanceId, Guid TenantId);

    public sealed record OwnerSummary(
        short OwnerSequence,
        string DocumentTypeCode,
        string DocumentNumber,
        decimal OwnershipPercentage);

    public sealed record Response(
        bool IsValid,
        decimal TotalPercentage,
        IReadOnlyList<OwnerSummary> Owners,
        IReadOnlyList<string> Errors);

    public enum ErrorKind
    {
        InstanceNotFound,
    }

    public sealed record ValidationError(ErrorKind Kind, string Message);

    public static async Task<Result<Response, ValidationError>> HandleAsync(
        Query query,
        IProcedureInstanceRepository instanceRepo,
        IProcedureActorRepository actorRepo,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(query.ProcedureInstanceId, query.TenantId, ct);
        if (instance is null)
        {
            return Result<Response, ValidationError>.Failure(
                new ValidationError(ErrorKind.InstanceNotFound, "Instancia de trámite no encontrada."));
        }

        var actors = await actorRepo.ListByInstanceAndEdgeAsync(
            query.TenantId,
            query.ProcedureInstanceId,
            SaveProcedureOwners.PropietarioEdgeRole,
            ct);

        var errors = new List<string>();
        var owners = new List<OwnerSummary>();
        decimal total = 0m;

        foreach (var actor in actors.OrderBy(a => a.OwnerSequence))
        {
            if (!actor.OwnershipPercentage.HasValue)
            {
                errors.Add($"Propietario secuencia {actor.OwnerSequence}: ownershipPercentage es requerido.");
                continue;
            }

            if (actor.OwnershipPercentage.Value is <= 0 or > 100)
            {
                errors.Add($"Propietario secuencia {actor.OwnerSequence}: porcentaje debe estar entre 0.01 y 100.");
            }

            total += actor.OwnershipPercentage.Value;
            owners.Add(new OwnerSummary(
                actor.OwnerSequence,
                actor.DocumentTypeCode,
                actor.DocumentNumber,
                actor.OwnershipPercentage.Value));
        }

        if (owners.Count == 0)
        {
            errors.Add("No hay propietarios registrados.");
        }
        else if (total != 100m)
        {
            errors.Add($"La suma de porcentajes debe ser 100.00; actual: {total:F2}.");
        }

        var isValid = errors.Count == 0;
        return Result<Response, ValidationError>.Success(
            new Response(isValid, total, owners, errors));
    }
}
