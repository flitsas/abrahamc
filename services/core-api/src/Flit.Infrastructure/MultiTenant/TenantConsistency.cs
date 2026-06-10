namespace Flit.Infrastructure.MultiTenant;

/// <summary>
/// Validación CF-D7 / AC2 — tenant del recurso vs sesión (HU #9414 base para APIs posteriores).
/// </summary>
public static class TenantConsistency
{
    public const string ErrorCode = "TENANT_MISMATCH";

    public static TenantMismatchError? TryGetMismatch(Guid? resourceTenantId, ITenantContext session)
    {
        if (session.IsSuperAdmin)
            return null;

        if (!resourceTenantId.HasValue || !session.TenantId.HasValue)
            return null;

        if (resourceTenantId.Value == session.TenantId.Value)
            return null;

        return new TenantMismatchError(
            ErrorCode,
            "El tenant del recurso no coincide con el contexto de la sesión.");
    }
}

public sealed record TenantMismatchError(string Code, string Message);
