using Flit.Modules.Procedures.Ports;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.Procedures.Application;

/// <summary>
/// TRA-02 #9434 — persiste actores y vehículo capturados al radicar
/// (<c>procedure_actors</c>, <c>procedure_vehicles</c>).
/// </summary>
public static class SaveProcedureRuntimeCapture
{
    private static readonly HashSet<string> ActorEdges =
        new(StringComparer.OrdinalIgnoreCase) { "propietario", "comprador", "locatario" };

    private static readonly string[] PrimaryActorEdgePriority = ["comprador", "propietario", "locatario"];

    public sealed record Command(
        Guid TenantId,
        Guid ProcedureInstanceId,
        Guid UserId,
        string ProcedureTypeCode,
        Guid? TrafficAgencyId,
        string EdgeCode,
        string DocumentTypeCode,
        string? PersonKind,
        IReadOnlyDictionary<string, string?>? CapturedFields);

    public sealed record Result(
        Guid? PrimaryActorId,
        Guid? VehicleId,
        string? PrimaryActorEdgeRole);

    public static async Task<Result> HandleAsync(
        Command command,
        IProceduresConfigReadRepository configRepo,
        IDocumentTypesReadRepository documentTypesRepo,
        IProcedureActorRepository actorRepo,
        IProcedureVehicleRepository vehicleRepo,
        CancellationToken ct = default)
    {
        var fields = command.CapturedFields ?? new Dictionary<string, string?>();
        var config = await GetProcedureConfiguration.HandleAsync(
            new GetProcedureConfiguration.Query(
                command.TenantId, command.ProcedureTypeCode, command.TrafficAgencyId),
            configRepo,
            ct);

        if (config is null)
        {
            return new Result(null, null, null);
        }

        var vehicleId = await SaveVehicleAsync(command, fields, vehicleRepo, ct);

        var savedActors = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in config.Edges.Where(e => e.IsActive && ActorEdges.Contains(e.Code)))
        {
            var actorId = await SaveActorEdgeAsync(
                command, edge.Code, fields, configRepo, documentTypesRepo, actorRepo, ct);
            if (actorId.HasValue)
            {
                savedActors[edge.Code] = actorId.Value;
            }
        }

        foreach (var edgeCode in PrimaryActorEdgePriority)
        {
            if (savedActors.TryGetValue(edgeCode, out var actorId))
            {
                return new Result(actorId, vehicleId, edgeCode.ToLowerInvariant());
            }
        }

        return new Result(null, vehicleId, null);
    }

    private static async Task<Guid?> SaveActorEdgeAsync(
        Command command,
        string edgeCode,
        IReadOnlyDictionary<string, string?> fields,
        IProceduresConfigReadRepository configRepo,
        IDocumentTypesReadRepository documentTypesRepo,
        IProcedureActorRepository actorRepo,
        CancellationToken ct)
    {
        var docNumber = ResolveDocumentNumber(fields, edgeCode);
        if (string.IsNullOrWhiteSpace(docNumber))
        {
            return null;
        }

        var docTypeCode = ResolveDocumentTypeCode(fields, edgeCode, command.DocumentTypeCode);

        var (incorporation, error) = await IncorporateProcedureActors.HandleAsync(
            new IncorporateProcedureActors.Command(
                command.TenantId,
                command.ProcedureTypeCode,
                command.TrafficAgencyId,
                edgeCode,
                docTypeCode,
                fields),
            configRepo,
            documentTypesRepo,
            ct);

        if (error is not null || incorporation is null)
        {
            return null;
        }

        var fullName = FirstNonEmpty(fields, "nombre", "fullName", "razon_social");
        var edgeRole = edgeCode.ToLowerInvariant();

        var existing = await actorRepo.GetByInstanceAndEdgeAsync(
            command.TenantId, command.ProcedureInstanceId, edgeRole, ct);
        var actorId = existing?.Id ?? Guid.NewGuid();

        await actorRepo.UpsertAsync(
            new ProcedureActorEntry(
                actorId,
                command.TenantId,
                command.ProcedureInstanceId,
                edgeRole,
                incorporation.PersonKind,
                docTypeCode,
                docNumber.Trim(),
                fullName,
                OwnershipPercentage: null,
                OwnerSequence: 0,
                command.UserId,
                command.UserId),
            ct);

        return actorId;
    }

    private static async Task<Guid?> SaveVehicleAsync(
        Command command,
        IReadOnlyDictionary<string, string?> fields,
        IProcedureVehicleRepository vehicleRepo,
        CancellationToken ct)
    {
        var plate = NormalizePlate(FirstNonEmpty(fields, "placa", "licensePlate", "noPlaca"));
        var vin = FirstNonEmpty(fields, "vin", "numero_vin");
        if (string.IsNullOrWhiteSpace(plate) && string.IsNullOrWhiteSpace(vin))
        {
            return null;
        }

        var existing = await vehicleRepo.GetByInstanceAsync(
            command.TenantId, command.ProcedureInstanceId, ct);
        var vehicleId = existing?.Id ?? Guid.NewGuid();

        await vehicleRepo.UpsertAsync(
            new ProcedureVehicleEntry(
                vehicleId,
                command.TenantId,
                command.ProcedureInstanceId,
                "automovil",
                plate,
                vin,
                command.UserId,
                command.UserId),
            ct);

        return vehicleId;
    }

    internal static string? ResolveDocumentNumber(
        IReadOnlyDictionary<string, string?> fields,
        string edgeCode)
    {
        if (fields.TryGetValue($"doc_{edgeCode}", out var edgeDoc) && !string.IsNullOrWhiteSpace(edgeDoc))
        {
            return edgeDoc;
        }

        if (string.Equals(edgeCode, "propietario", StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(fields, "doc_propietario", "numero_documento", "documentNumber", "nit");
        }

        if (string.Equals(edgeCode, "locatario", StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(fields, "doc_locatario", "numero_documento", "documentNumber");
        }

        return null;
    }

    internal static string ResolveDocumentTypeCode(
        IReadOnlyDictionary<string, string?> fields,
        string edgeCode,
        string fallback)
    {
        if (fields.TryGetValue($"tipo_documento_{edgeCode}", out var perEdge) && !string.IsNullOrWhiteSpace(perEdge))
        {
            return perEdge.Trim();
        }

        if (fields.TryGetValue("tipo_documento", out var shared) && !string.IsNullOrWhiteSpace(shared))
        {
            return shared.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "CC" : fallback;
    }

    private static string? NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return null;
        }

        return plate.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private static string? FirstNonEmpty(
        IReadOnlyDictionary<string, string?> fields,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
