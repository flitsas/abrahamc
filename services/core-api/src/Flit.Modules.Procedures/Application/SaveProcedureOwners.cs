using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Procedures.Application;

/// <summary>HU #10081 — persistencia de copropietarios con porcentaje.</summary>
public static class SaveProcedureOwners
{
    public const string PropietarioEdgeRole = "propietario";

    public sealed record OwnerInput(
        string DocumentTypeCode,
        string DocumentNumber,
        string? FullName,
        decimal? OwnershipPercentage,
        short OwnerSequence);

    public sealed record Command(
        Guid ProcedureInstanceId,
        Guid TenantId,
        Guid SavedByUserId,
        IReadOnlyList<OwnerInput> Owners);

    public sealed record Response(
        Guid ProcedureInstanceId,
        int OwnerCount);

    public enum ErrorKind
    {
        InstanceNotFound,
        InvalidOwners,
    }

    public sealed record SaveOwnersError(ErrorKind Kind, string Message);

    public static async Task<Result<Response, SaveOwnersError>> HandleAsync(
        Command command,
        IProcedureInstanceRepository instanceRepo,
        IProcedureActorRepository actorRepo,
        CancellationToken ct = default)
    {
        var instance = await instanceRepo.GetByIdAsync(command.ProcedureInstanceId, command.TenantId, ct);
        if (instance is null)
        {
            return Result<Response, SaveOwnersError>.Failure(
                new SaveOwnersError(ErrorKind.InstanceNotFound, "Instancia de trámite no encontrada."));
        }

        if (command.Owners.Count == 0)
        {
            return Result<Response, SaveOwnersError>.Failure(
                new SaveOwnersError(ErrorKind.InvalidOwners, "Debe registrar al menos un propietario."));
        }

        var sequences = command.Owners.Select(o => o.OwnerSequence).ToList();
        if (sequences.Distinct().Count() != sequences.Count)
        {
            return Result<Response, SaveOwnersError>.Failure(
                new SaveOwnersError(ErrorKind.InvalidOwners, "ownerSequence debe ser único por propietario."));
        }

        foreach (var owner in command.Owners)
        {
            if (string.IsNullOrWhiteSpace(owner.DocumentTypeCode) || string.IsNullOrWhiteSpace(owner.DocumentNumber))
            {
                return Result<Response, SaveOwnersError>.Failure(
                    new SaveOwnersError(ErrorKind.InvalidOwners, "documentTypeCode y documentNumber son requeridos."));
            }
        }

        var entries = command.Owners.Select(owner => new ProcedureActorEntry(
            Id: Guid.NewGuid(),
            TenantId: command.TenantId,
            ProcedureInstanceId: command.ProcedureInstanceId,
            EdgeRole: PropietarioEdgeRole,
            PersonKind: "natural",
            DocumentTypeCode: owner.DocumentTypeCode.Trim(),
            DocumentNumber: owner.DocumentNumber.Trim(),
            FullName: owner.FullName,
            OwnershipPercentage: owner.OwnershipPercentage,
            OwnerSequence: owner.OwnerSequence,
            CreatedBy: command.SavedByUserId,
            UpdatedBy: command.SavedByUserId)).ToList();

        await actorRepo.ReplaceOwnersAsync(
            command.TenantId,
            command.ProcedureInstanceId,
            PropietarioEdgeRole,
            entries,
            ct);

        return Result<Response, SaveOwnersError>.Success(
            new Response(command.ProcedureInstanceId, entries.Count));
    }
}
