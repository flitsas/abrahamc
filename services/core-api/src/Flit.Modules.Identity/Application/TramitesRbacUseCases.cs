using System.Text.RegularExpressions;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Application;

/// <summary>CRUD de slugs y consulta de permisos efectivos (HU #9417).</summary>
public static class TramitesRbacUseCases
{
    public const string ForbiddenCode = "FORBIDDEN";
    public const string SlugFormatCode = "INVALID_SLUG_FORMAT";
    public const string SlugExistsCode = "SLUG_ALREADY_EXISTS";
    public const string NotFoundCode = "PERMISSION_NOT_FOUND";

    private static readonly Regex SlugPattern = new(
        @"^[a-z0-9]+(\.[a-z0-9-]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public sealed record CreatePermissionCommand(
        string Slug,
        string Module,
        string Action,
        string? Description);

    public static bool IsValidSlug(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && SlugPattern.IsMatch(slug.Trim());

    public static async Task<Result<PermissionSlugRow, string>> CreatePermissionAsync(
        CreatePermissionCommand cmd,
        IIdentityRbacRepository rbac,
        Guid? createdBy,
        CancellationToken ct = default)
    {
        var slug = cmd.Slug.Trim().ToLowerInvariant();
        if (!IsValidSlug(slug))
            return Result<PermissionSlugRow, string>.Failure(SlugFormatCode);

        if (await rbac.GetPermissionBySlugAsync(slug, ct) is not null)
            return Result<PermissionSlugRow, string>.Failure(SlugExistsCode);

        var row = await rbac.CreatePermissionAsync(
            slug,
            cmd.Module.Trim().ToLowerInvariant(),
            cmd.Action.Trim().ToLowerInvariant(),
            cmd.Description?.Trim(),
            createdBy,
            ct);

        return Result<PermissionSlugRow, string>.Success(row);
    }

    public static async Task<Result<PermissionSlugRow, string>> SetActiveAsync(
        Guid id,
        bool isActive,
        IIdentityRbacRepository rbac,
        CancellationToken ct = default)
    {
        var existing = await rbac.GetPermissionByIdAsync(id, ct);
        if (existing is null)
            return Result<PermissionSlugRow, string>.Failure(NotFoundCode);

        if (!await rbac.SetPermissionActiveAsync(id, isActive, ct))
            return Result<PermissionSlugRow, string>.Failure(NotFoundCode);

        return Result<PermissionSlugRow, string>.Success(existing with { IsActive = isActive });
    }
}
