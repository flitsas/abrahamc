namespace Flit.Modules.ProceduresConfig.Ports;

public sealed record IdentityEvidenceBlob(string Label, byte[] ImageBytes);

/// <summary>Evidencia biométrica aprobada para anexar al consolidado (DOC-02 #9443).</summary>
public interface IApprovedIdentityEvidencePort
{
    Task<IReadOnlyList<IdentityEvidenceBlob>> ListApprovedForInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
