using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Resolución de tenant efectivo para consolas SA/TA (HU #9419, AC1/AC2).</summary>
public static class TramitesAdminContext
{
    public const string TenantMismatchCode = "TENANT_MISMATCH";
    public const string TenantContextRequiredCode = "TENANT_CONTEXT_REQUIRED";

    public static Result<Guid, string> ResolveEffectiveTenant(
        Guid? sessionTenantId,
        bool isSuperAdmin,
        Guid? requestTenantId)
    {
        if (!sessionTenantId.HasValue && !requestTenantId.HasValue)
            return Result<Guid, string>.Failure(TenantContextRequiredCode);

        if (!isSuperAdmin)
        {
            var session = sessionTenantId!.Value;
            if (requestTenantId.HasValue && requestTenantId.Value != session)
                return Result<Guid, string>.Failure(TenantMismatchCode);

            return Result<Guid, string>.Success(session);
        }

        var effective = requestTenantId ?? sessionTenantId;
        if (!effective.HasValue)
            return Result<Guid, string>.Failure(TenantContextRequiredCode);

        return Result<Guid, string>.Success(effective.Value);
    }
}
