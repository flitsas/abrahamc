using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.Notifications.Ports;
using Flit.Modules.Rbac.Application;
using Flit.Modules.Rbac.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// Panel backoffice IDSecure — HU #9487.
/// </summary>
public static class IdSecureBackofficeEndpoints
{
    public sealed record ManualOverrideRequest(string NewVerdict, string Reason);

    public static void MapIdSecureBackofficeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/idsecure/review").WithTags("IDSecure - Backoffice");

        group.MapGet("/panel", async (
            Guid userId,
            Guid tenantId,
            Guid? procedureTypeId,
            ITenantContext tenantContext,
            IPermissionsRepository permissionsRepo,
            IPermissionsCache permissionsCache,
            IIdSecureReviewReadRepository readRepo,
            IVerificationAiVerdictRepository verdictRepo,
            IVerificationManualOverrideRepository overrideRepo,
            CancellationToken ct) =>
        {
            tenantContext.SetTenant(tenantId);

            var hasPermission = await GetUserPermissions.UserHasPermissionAsync(
                userId,
                IdSecurePermissions.Review,
                permissionsRepo,
                permissionsCache,
                ct);

            var result = await ListIdSecureReviewPanel.HandleAsync(
                new ListIdSecureReviewPanel.Query(tenantId, procedureTypeId),
                hasPermission,
                readRepo,
                verdictRepo,
                overrideRepo,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(r),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureReviewPanel");

        group.MapPost("/sessions/{sessionId:guid}/override", async (
            Guid sessionId,
            Guid userId,
            Guid tenantId,
            ManualOverrideRequest body,
            ITenantContext tenantContext,
            IPermissionsRepository permissionsRepo,
            IPermissionsCache permissionsCache,
            IVerificationSessionRepository sessionRepo,
            IVerificationAiVerdictRepository verdictRepo,
            IVerificationManualOverrideRepository overrideRepo,
            IProcedureIdentityValidationRepository validationRepo,
            IIdentityVerificationAuditRepository auditRepo,
            IIdentityVerificationActivationRepository activationRepo,
            INotificationsPublisher notificationsPublisher,
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

            var result = await ApplyManualVerdictOverride.HandleAsync(
                new ApplyManualVerdictOverride.Command(
                    tenantId,
                    sessionId,
                    body.NewVerdict,
                    body.Reason,
                    userId),
                hasPermission,
                sessionRepo,
                verdictRepo,
                overrideRepo,
                validationRepo,
                auditRepo,
                clock,
                ct);

            if (result.IsSuccess)
            {
                var session = await sessionRepo.GetByIdAsync(tenantId, sessionId, ct);
                if (session?.ProcedureInstanceId is Guid instanceId)
                {
                    await IdentityGateNotificationHelper.NotifyIfPossibleAsync(
                        tenantId,
                        instanceId,
                        activationRepo,
                        sessionRepo,
                        notificationsPublisher,
                        clock,
                        ct);
                }
            }

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(r),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureManualOverride");
    }
}
