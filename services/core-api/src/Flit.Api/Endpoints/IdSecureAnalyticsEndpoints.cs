using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.Rbac.Application;
using Flit.Modules.Rbac.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// Analytics IDSecure People &amp; Fraud — HU #9489.
/// </summary>
public static class IdSecureAnalyticsEndpoints
{
    public static void MapIdSecureAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/idsecure/analytics").WithTags("IDSecure - Analytics");

        group.MapGet("/people-fraud", async (
            Guid userId,
            Guid tenantId,
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
                IdSecurePermissions.Analytics,
                permissionsRepo,
                permissionsCache,
                ct);

            var result = await GetIdSecurePeopleFraudAnalytics.HandleAsync(
                new GetIdSecurePeopleFraudAnalytics.Query(tenantId),
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
        .WithName("IdSecurePeopleFraudAnalytics");
    }
}
