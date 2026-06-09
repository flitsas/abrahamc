using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

public sealed class ApprovedIdentityEvidenceAdapter(
    IProcedureIdentityValidationRepository identityValidations,
    IVerificationEvidenceRepository evidenceRepo,
    IIdSecureFileRepository idSecureFiles,
    IIdSecureBlobStorage? blobStorage) : IApprovedIdentityEvidencePort
{
    public async Task<IReadOnlyList<IdentityEvidenceBlob>> ListApprovedForInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var validations = await identityValidations.GetByProcedureInstanceIdAsync(
            tenantId,
            procedureInstanceId,
            ct);

        var images = new List<IdentityEvidenceBlob>();

        foreach (var validation in validations.Where(v =>
                     string.Equals(v.Verdict, IdentityValidationVerdict.Approved, StringComparison.OrdinalIgnoreCase)))
        {
            var evidences = await evidenceRepo.ListBySessionIdAsync(
                tenantId,
                validation.VerificationSessionId,
                ct);

            foreach (var evidence in evidences.OrderBy(e => e.EvidenceType, StringComparer.Ordinal))
            {
                var file = await idSecureFiles.GetByIdAsync(tenantId, evidence.FileId, ct);
                if (file is null || file.Status != FileStatuses.Ready)
                    continue;

                byte[]? bytes = null;
                if (blobStorage is not null)
                    bytes = await blobStorage.TryGetAsync(file.Bucket, file.ObjectKey, ct);

                if (bytes is null || bytes.Length == 0)
                    continue;

                images.Add(new IdentityEvidenceBlob(
                    $"Identidad — {evidence.EvidenceType}",
                    bytes));
            }
        }

        return images;
    }
}
