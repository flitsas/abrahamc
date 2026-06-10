using System.Text.RegularExpressions;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>Consolas Super Admin / Tenant Admin (HU #9419).</summary>
public static class TramitesAdminUseCases
{
    public const string ForbiddenRoleCode = "FORBIDDEN_ROLE";
    public const string EmailExistsCode = "EMAIL_ALREADY_REGISTERED";
    public const string RoleNotFoundCode = "ROLE_NOT_FOUND";
    public const string UserNotFoundCode = "USER_NOT_FOUND";
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string SlugConflictCode = "SLUG_CONFLICT";
    public const string NitConflictCode = "NIT_CONFLICT";
    public const string InvalidSlugCode = "INVALID_SLUG";
    public const string InvalidAccountStateCode = "INVALID_ACCOUNT_STATE";
    public const string ForbiddenSuperAdminOnlyCode = "SUPER_ADMIN_REQUIRED";

    private static readonly Regex TenantSlugRegex = new(
        "^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedAccountStates = new(StringComparer.Ordinal)
    {
        "active", "inactive", "temp_blocked", "permanent_blocked",
    };

    public sealed record ActorContext(
        Guid UserId,
        Guid? SessionTenantId,
        bool IsSuperAdmin);

    public sealed record CreateTenantCommand(string Name, string Nit, string Slug, string? SettingsJson);

    public sealed record UpdateTenantCommand(
        Guid Id,
        string? Name,
        string? Status,
        string? SettingsJson);

    public sealed record CreateCollaboratorCommand(
        string Email,
        string FullName,
        string? Phone,
        Guid? RoleId,
        Guid? BodyTenantId);

    public sealed record UpdateCollaboratorCommand(
        Guid UserId,
        string? FullName,
        string? Phone,
        string? AccountState);

    public sealed record SessionContextSuccess(
        Guid EffectiveTenantId,
        string? TenantName,
        bool IsSuperAdmin,
        Guid ActorUserId);

    public static async Task<Result<SessionContextSuccess, string>> GetSessionContextAsync(
        ActorContext actor,
        Guid? requestTenantId,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        var resolved = TramitesAdminContext.ResolveEffectiveTenant(
            actor.SessionTenantId, actor.IsSuperAdmin, requestTenantId);
        if (!resolved.IsSuccess)
            return Result<SessionContextSuccess, string>.Failure(resolved.Error);

        var tenantId = resolved.Value;
        await admin.ApplySessionGucAsync(
            tenantId, actor.UserId, actor.IsSuperAdmin, ct);
        var name = await admin.GetTenantNameAsync(tenantId, ct);

        return Result<SessionContextSuccess, string>.Success(
            new SessionContextSuccess(tenantId, name, actor.IsSuperAdmin, actor.UserId));
    }

    public static async Task<Result<IReadOnlyList<TenantRow>, string>> ListTenantsAsync(
        ActorContext actor,
        string? status,
        string? search,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        if (!actor.IsSuperAdmin)
            return Result<IReadOnlyList<TenantRow>, string>.Failure(ForbiddenSuperAdminOnlyCode);

        var list = await admin.ListTenantsAsync(status, search, ct);
        return Result<IReadOnlyList<TenantRow>, string>.Success(list);
    }

    public static async Task<Result<TenantRow, string>> GetTenantAsync(
        ActorContext actor,
        Guid id,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        if (!actor.IsSuperAdmin)
            return Result<TenantRow, string>.Failure(ForbiddenSuperAdminOnlyCode);

        var row = await admin.GetTenantByIdAsync(id, ct);
        return row is null
            ? Result<TenantRow, string>.Failure(TenantNotFoundCode)
            : Result<TenantRow, string>.Success(row);
    }

    public static async Task<Result<TenantRow, string>> CreateTenantAsync(
        ActorContext actor,
        CreateTenantCommand cmd,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        if (!actor.IsSuperAdmin)
            return Result<TenantRow, string>.Failure(ForbiddenSuperAdminOnlyCode);

        var slug = cmd.Slug.Trim().ToLowerInvariant();
        if (!TenantSlugRegex.IsMatch(slug))
            return Result<TenantRow, string>.Failure(InvalidSlugCode);

        if (await admin.TenantSlugExistsAsync(slug, null, ct))
            return Result<TenantRow, string>.Failure(SlugConflictCode);

        if (await admin.TenantNitExistsAsync(cmd.Nit.Trim(), null, ct))
            return Result<TenantRow, string>.Failure(NitConflictCode);

        var row = await admin.CreateTenantAsync(
            cmd.Name.Trim(),
            cmd.Nit.Trim(),
            slug,
            cmd.SettingsJson,
            actor.UserId,
            ct);

        return Result<TenantRow, string>.Success(row);
    }

    public static async Task<Result<TenantRow, string>> UpdateTenantAsync(
        ActorContext actor,
        UpdateTenantCommand cmd,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        if (!actor.IsSuperAdmin)
            return Result<TenantRow, string>.Failure(ForbiddenSuperAdminOnlyCode);

        if (cmd.Status is not null &&
            cmd.Status is not "active" and not "inactive" and not "suspended")
            return Result<TenantRow, string>.Failure(InvalidAccountStateCode);

        var row = await admin.UpdateTenantAsync(
            cmd.Id, cmd.Name?.Trim(), cmd.Status, cmd.SettingsJson, actor.UserId, ct);

        return row is null
            ? Result<TenantRow, string>.Failure(TenantNotFoundCode)
            : Result<TenantRow, string>.Success(row);
    }

    public static async Task<Result<(IReadOnlyList<CollaboratorRow> Items, int Total), string>>
        ListCollaboratorsAsync(
            ActorContext actor,
            Guid? requestTenantId,
            int page,
            int limit,
            string? search,
            IIdentityAdminRepository admin,
            CancellationToken ct = default)
    {
        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<(IReadOnlyList<CollaboratorRow>, int), string>.Failure(tenantResult.Error);

        var safePage = Math.Max(1, page);
        var safeLimit = Math.Clamp(limit, 1, 100);
        var list = await admin.ListCollaboratorsAsync(
            tenantResult.Value, safePage, safeLimit, search?.Trim(), ct);

        return Result<(IReadOnlyList<CollaboratorRow>, int), string>.Success(list);
    }

    public static async Task<Result<CollaboratorRow, string>> GetCollaboratorAsync(
        ActorContext actor,
        Guid? requestTenantId,
        Guid userId,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<CollaboratorRow, string>.Failure(tenantResult.Error);

        var row = await admin.GetCollaboratorAsync(tenantResult.Value, userId, ct);
        return row is null
            ? Result<CollaboratorRow, string>.Failure(UserNotFoundCode)
            : Result<CollaboratorRow, string>.Success(row);
    }

    public static async Task<Result<CollaboratorRow, string>> CreateCollaboratorAsync(
        ActorContext actor,
        Guid? requestTenantId,
        CreateCollaboratorCommand cmd,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        if (!actor.IsSuperAdmin && cmd.BodyTenantId.HasValue)
        {
            var mismatch = TramitesAdminContext.ResolveEffectiveTenant(
                actor.SessionTenantId, false, cmd.BodyTenantId);
            if (!mismatch.IsSuccess)
                return Result<CollaboratorRow, string>.Failure(mismatch.Error);
        }

        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<CollaboratorRow, string>.Failure(tenantResult.Error);

        var tenantId = tenantResult.Value;
        var email = cmd.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || !email.Contains('@', StringComparison.Ordinal))
            return Result<CollaboratorRow, string>.Failure("INVALID_EMAIL");

        if (await admin.EmailExistsGloballyAsync(email, ct))
            return Result<CollaboratorRow, string>.Failure(EmailExistsCode);

        if (cmd.RoleId.HasValue)
        {
            var roleError = await ValidateRoleAssignmentAsync(
                actor, cmd.RoleId.Value, admin, ct);
            if (roleError is not null)
                return Result<CollaboratorRow, string>.Failure(roleError);
        }

        var userId = await admin.CreateCollaboratorUserAsync(tenantId, email, actor.UserId, ct);
        await admin.CreateCollaboratorProfileAsync(
            tenantId, userId, cmd.FullName.Trim(), cmd.Phone?.Trim(), actor.UserId, ct);

        if (cmd.RoleId.HasValue)
            await admin.EnsureUserRoleAsync(tenantId, userId, cmd.RoleId.Value, actor.UserId, ct);

        var created = await admin.GetCollaboratorAsync(tenantId, userId, ct);
        return created is null
            ? Result<CollaboratorRow, string>.Failure(UserNotFoundCode)
            : Result<CollaboratorRow, string>.Success(created);
    }

    public static async Task<Result<CollaboratorRow, string>> UpdateCollaboratorAsync(
        ActorContext actor,
        Guid? requestTenantId,
        UpdateCollaboratorCommand cmd,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<CollaboratorRow, string>.Failure(tenantResult.Error);

        if (cmd.AccountState is not null && !AllowedAccountStates.Contains(cmd.AccountState))
            return Result<CollaboratorRow, string>.Failure(InvalidAccountStateCode);

        var updated = await admin.UpdateCollaboratorAsync(
            tenantResult.Value,
            cmd.UserId,
            cmd.FullName?.Trim(),
            cmd.Phone?.Trim(),
            cmd.AccountState,
            actor.UserId,
            ct);

        if (!updated)
            return Result<CollaboratorRow, string>.Failure(UserNotFoundCode);

        var row = await admin.GetCollaboratorAsync(tenantResult.Value, cmd.UserId, ct);
        return row is null
            ? Result<CollaboratorRow, string>.Failure(UserNotFoundCode)
            : Result<CollaboratorRow, string>.Success(row);
    }

    public static async Task<Result<IReadOnlyList<AssignableRoleRow>, string>> ListAssignableRolesAsync(
        ActorContext actor,
        Guid? requestTenantId,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<IReadOnlyList<AssignableRoleRow>, string>.Failure(tenantResult.Error);

        var roles = await admin.ListAssignableRolesAsync(excludeSuperAdmin: !actor.IsSuperAdmin, ct);
        return Result<IReadOnlyList<AssignableRoleRow>, string>.Success(roles);
    }

    public static async Task<Result<Unit, string>> AssignRoleAsync(
        ActorContext actor,
        Guid? requestTenantId,
        Guid userId,
        Guid roleId,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<Unit, string>.Failure(tenantResult.Error);

        var roleError = await ValidateRoleAssignmentAsync(actor, roleId, admin, ct);
        if (roleError is not null)
            return Result<Unit, string>.Failure(roleError);

        var user = await admin.GetCollaboratorAsync(tenantResult.Value, userId, ct);
        if (user is null)
            return Result<Unit, string>.Failure(UserNotFoundCode);

        await admin.EnsureUserRoleAsync(tenantResult.Value, userId, roleId, actor.UserId, ct);
        return Result<Unit, string>.Success(default);
    }

    public static async Task<Result<Unit, string>> RemoveRoleAsync(
        ActorContext actor,
        Guid? requestTenantId,
        Guid userId,
        Guid roleId,
        IIdentityAdminRepository admin,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveAndApplyGucAsync(actor, requestTenantId, admin, ct);
        if (!tenantResult.IsSuccess)
            return Result<Unit, string>.Failure(tenantResult.Error);

        var removed = await admin.RemoveUserRoleAsync(
            tenantResult.Value, userId, roleId, actor.UserId, ct);

        return removed
            ? Result<Unit, string>.Success(default)
            : Result<Unit, string>.Failure(RoleNotFoundCode);
    }

    private static async Task<Result<Guid, string>> ResolveAndApplyGucAsync(
        ActorContext actor,
        Guid? requestTenantId,
        IIdentityAdminRepository admin,
        CancellationToken ct)
    {
        var resolved = TramitesAdminContext.ResolveEffectiveTenant(
            actor.SessionTenantId, actor.IsSuperAdmin, requestTenantId);
        if (!resolved.IsSuccess)
            return Result<Guid, string>.Failure(resolved.Error);

        await admin.ApplySessionGucAsync(
            resolved.Value, actor.UserId, actor.IsSuperAdmin, ct);
        return Result<Guid, string>.Success(resolved.Value);
    }

    private static async Task<string?> ValidateRoleAssignmentAsync(
        ActorContext actor,
        Guid roleId,
        IIdentityAdminRepository admin,
        CancellationToken ct)
    {
        var role = await admin.GetRoleByIdAsync(roleId, ct);
        if (role is null)
            return RoleNotFoundCode;

        if (!actor.IsSuperAdmin && role.Slug == "super-admin")
            return ForbiddenRoleCode;

        if (!actor.IsSuperAdmin &&
            role.Slug is not "tenant-admin" and not "colaborador")
            return ForbiddenRoleCode;

        return null;
    }
}

/// <summary>Marcador vacío para Result exitoso sin payload.</summary>
public readonly struct Unit;
