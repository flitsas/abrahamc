using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// Endpoints operador IDSecure en detalle de trámite — HU #9488.
/// </summary>
public static class IdSecureOperatorEndpoints
{
    public static void MapIdSecureOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/idsecure/instances")
            .WithTags("IDSecure - Operator");

        group.MapGet("/{procedureInstanceId:guid}/identity-gate", async (
            Guid procedureInstanceId,
            Guid tenantId,
            IIdentityVerificationActivationRepository activationRepo,
            IVerificationSessionRepository sessionRepo,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            tenantContext.SetTenant(tenantId);

            var result = await ProcedureIdentityGate.GetStatusAsync(
                tenantId,
                procedureInstanceId,
                activationRepo,
                sessionRepo,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(r),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureOperatorIdentityGate");
    }
}
