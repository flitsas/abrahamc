using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9490 AC1 — Purga evidencias biométricas vencidas (MinIO + anonimización metadatos).
/// </summary>
public static class PurgeExpiredIdSecureEvidence
{
    public sealed record Command(int RetentionDays);

    public sealed record Response(
        int PurgedEvidenceCount,
        int DeletedBlobCount,
        int AnonymizedSessionCount);

    public static async Task<Response> HandleAsync(
        Command cmd,
        IVerificationEvidenceRepository evidenceRepo,
        IIdSecureFileRepository fileRepo,
        IVerificationSessionRepository sessionRepo,
        IVerificationOcrResultRepository ocrRepo,
        IIdSecureBlobStorage blobStorage,
        IClock clock,
        CancellationToken ct = default)
    {
        var retentionDays = cmd.RetentionDays > 0
            ? cmd.RetentionDays
            : IdSecureRetentionPolicy.DefaultRetentionDays;

        var cutoff = IdSecureRetentionPolicy.ComputeCutoff(clock.UtcNow, retentionDays);
        var expired = await evidenceRepo.ListExpiredAsync(cutoff, ct);

        var platformActor = Guid.Parse("00000000-0000-7000-8000-000000000000");
        var now = clock.UtcNow;
        var deletedBlobs = 0;
        var anonymizedSessions = new HashSet<Guid>();

        foreach (var evidence in expired)
        {
            var file = await fileRepo.GetByIdAsync(evidence.TenantId, evidence.FileId, ct);
            if (file is not null && file.Status != FileStatuses.Purged)
            {
                if (await blobStorage.ObjectExistsAsync(file.Bucket, file.ObjectKey, ct))
                {
                    await blobStorage.DeleteObjectAsync(file.Bucket, file.ObjectKey, ct);
                    deletedBlobs++;
                }

                file.AnonymizeAfterPurge(now, platformActor);
                await fileRepo.UpdateAsync(file, ct);
            }

            evidence.AnonymizeAfterPurge(now, platformActor);
            await evidenceRepo.UpdateAsync(evidence, ct);

            var session = await sessionRepo.GetByIdAsync(
                evidence.TenantId, evidence.VerificationSessionId, ct);
            if (session is not null && session.SubjectDocumentNumber != "REDACTED")
            {
                session.AnonymizeSubjectPii(now, platformActor);
                await sessionRepo.UpdateAsync(session, ct);
                anonymizedSessions.Add(session.Id);

                var ocr = await ocrRepo.FindBySessionIdAsync(
                    evidence.TenantId, session.Id, ct);
                if (ocr is not null)
                {
                    ocr.AnonymizeAfterPurge();
                    await ocrRepo.UpdateAsync(ocr, ct);
                }
            }
        }

        await evidenceRepo.SaveChangesAsync(ct);
        await fileRepo.SaveChangesAsync(ct);
        await sessionRepo.SaveChangesAsync(ct);
        await ocrRepo.SaveChangesAsync(ct);

        return new Response(expired.Count, deletedBlobs, anonymizedSessions.Count);
    }
}
