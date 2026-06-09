using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.Rbac.Application;
using Flit.Modules.Rbac.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// Habeas Data IDSecure — retención, purga y acceso PII auditado (HU #9490).
/// </summary>
public static class IdSecureHabeasDataEndpoints
{
    public static void MapIdSecureHabeasDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/idsecure/habeas-data").WithTags("IDSecure - Habeas Data");

        group.MapPost("/purge-expired-evidence", async (
            int? retentionDays,
            IVerificationEvidenceRepository evidenceRepo,
            IIdSecureFileRepository fileRepo,
            IVerificationSessionRepository sessionRepo,
            IVerificationOcrResultRepository ocrRepo,
            IIdSecureBlobStorage blobStorage,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await PurgeExpiredIdSecureEvidence.HandleAsync(
                new PurgeExpiredIdSecureEvidence.Command(retentionDays ?? IdSecureRetentionPolicy.DefaultRetentionDays),
                evidenceRepo,
                fileRepo,
                sessionRepo,
                ocrRepo,
                blobStorage,
                clock,
                ct);

            return Results.Ok(result);
        })
        .WithName("IdSecurePurgeExpiredEvidence");

        group.MapGet("/sessions/{sessionId:guid}/pii-record", async (
            Guid sessionId,
            Guid userId,
            Guid tenantId,
            string? purpose,
            Guid? requestId,
            string? ipAddress,
            ITenantContext tenantContext,
            IPermissionsRepository permissionsRepo,
            IPermissionsCache permissionsCache,
            IVerificationSessionRepository sessionRepo,
            IIdSecureReviewReadRepository readRepo,
            IVerificationEvidenceRepository evidenceRepo,
            IVerificationOcrResultRepository ocrRepo,
            IDataAccessLogRepository dataAccessLogRepo,
            IClock clock,
            CancellationToken ct) =>
        {
            tenantContext.SetTenant(tenantId);

            var hasPermission = await GetUserPermissions.UserHasPermissionAsync(
                userId,
                IdSecurePermissions.Review,
                permissionsRepo,
                permissionsCache,
                ct);

            var result = await GetIdSecureSessionPiiRecord.HandleAsync(
                new GetIdSecureSessionPiiRecord.Query(
                    tenantId,
                    sessionId,
                    userId,
                    purpose,
                    requestId,
                    ipAddress),
                hasPermission,
                sessionRepo,
                readRepo,
                evidenceRepo,
                ocrRepo,
                dataAccessLogRepo,
                clock,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(r),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureSessionPiiRecord");
    }
}
