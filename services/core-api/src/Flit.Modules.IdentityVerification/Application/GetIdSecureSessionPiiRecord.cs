using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9490 AC2 — Consulta ficha PII con registro en audit.data_access_log.
/// </summary>
public static class GetIdSecureSessionPiiRecord
{
    public sealed record Query(
        Guid TenantId,
        Guid VerificationSessionId,
        Guid OperatorUserId,
        string? Purpose,
        Guid? RequestId,
        string? IpAddress);

    public sealed record Response(
        Guid VerificationSessionId,
        string SubjectDocumentNumber,
        string ParticipantRole,
        string RecipientEmail,
        IReadOnlyList<string> EvidenceTypes,
        string? OcrDocumentNumber);

    public abstract record PiiRecordError(string Code, string Message, int HttpStatus)
    {
        public sealed record Forbidden()
            : PiiRecordError("FORBIDDEN", "Permiso idsecure.review requerido", 403);

        public sealed record SessionNotFound()
            : PiiRecordError("SESSION_NOT_FOUND", "Sesión no encontrada", 404);
    }

    public static async Task<Result<Response, PiiRecordError>> HandleAsync(
        Query query,
        bool hasReviewPermission,
        IVerificationSessionRepository sessionRepo,
        IIdSecureReviewReadRepository readRepo,
        IVerificationEvidenceRepository evidenceRepo,
        IVerificationOcrResultRepository ocrRepo,
        IDataAccessLogRepository dataAccessLogRepo,
        IClock clock,
        CancellationToken ct = default)
    {
        if (!hasReviewPermission)
            return Result<Response, PiiRecordError>.Failure(new PiiRecordError.Forbidden());

        var session = await sessionRepo.GetByIdAsync(
            query.TenantId, query.VerificationSessionId, ct);
        if (session is null)
            return Result<Response, PiiRecordError>.Failure(new PiiRecordError.SessionNotFound());

        var recipientEmail = string.Empty;
        if (session.VerificationInvitationId is Guid invitationId)
        {
            var invitation = await readRepo.FindInvitationByIdAsync(
                query.TenantId, invitationId, ct);
            recipientEmail = invitation?.RecipientEmail ?? string.Empty;
        }

        var evidences = await evidenceRepo.ListBySessionIdAsync(
            query.TenantId, session.Id, ct);
        var evidenceTypes = evidences
            .Select(e => e.EvidenceType)
            .Distinct()
            .ToList();

        string? ocrDocumentNumber = null;
        var ocr = await ocrRepo.FindBySessionIdAsync(query.TenantId, session.Id, ct);
        if (ocr is not null)
            ocrDocumentNumber = ExtractOcrField(ocr.ExtractedFieldsJson, OcrFieldKeys.DocumentNumber);

        await dataAccessLogRepo.AppendAsync(new DataAccessLogEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = query.TenantId,
            ActorUserId = query.OperatorUserId,
            SchemaName = IdSecureHabeasDataTables.Schema,
            TableName = IdSecureHabeasDataTables.VerificationSessions,
            RecordId = session.Id,
            AccessType = DataAccessTypes.Read,
            Purpose = query.Purpose ?? "idsecure_review_pii_record",
            AccessedAt = clock.UtcNow,
            RequestId = query.RequestId,
            IpAddress = query.IpAddress,
        }, ct);

        return Result<Response, PiiRecordError>.Success(
            new Response(
                session.Id,
                session.SubjectDocumentNumber,
                session.ParticipantRole ?? string.Empty,
                recipientEmail,
                evidenceTypes,
                ocrDocumentNumber));
    }

    internal static string? ExtractOcrField(string json, string key)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var needle = $"\"{key}\":\"";
        var start = json.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += needle.Length;
        var end = json.IndexOf('"', start);
        if (end < 0)
            return null;

        return json[start..end];
    }
}
