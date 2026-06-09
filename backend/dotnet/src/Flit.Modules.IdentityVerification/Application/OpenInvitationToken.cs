using System.Text;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9480 AC2 — abre URL de invitacion; token de un solo uso (410 si ya consumido).
/// </summary>
public static class OpenInvitationToken
{
    public sealed record Command(string PlainToken);

    public sealed record Response(
        Guid InvitationId,
        Guid TenantId,
        Guid ProcedureInstanceId,
        DateTimeOffset ExpiresAt);

    public abstract record OpenInvitationTokenError(string Code, string Message, int HttpStatus)
    {
        public sealed record NotFound()
            : OpenInvitationTokenError("TOKEN_NOT_FOUND", "Token invalido", 404);

        public sealed record AlreadyConsumed()
            : OpenInvitationTokenError("TOKEN_CONSUMED", "Token ya consumido", 410);

        public sealed record Expired()
            : OpenInvitationTokenError("TOKEN_EXPIRED", "Token expirado", 401);
    }

    public static async Task<Result<Response, OpenInvitationTokenError>> HandleAsync(
        Command cmd,
        IVerificationInvitationRepository repo,
        IInvitationTokenHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.PlainToken))
            return Result<Response, OpenInvitationTokenError>.Failure(new OpenInvitationTokenError.NotFound());

        var tokenHash = hasher.HashToken(Encoding.UTF8.GetBytes(cmd.PlainToken));
        var invitation = await repo.FindByTokenHashAsync(tokenHash, ct);
        if (invitation is null)
            return Result<Response, OpenInvitationTokenError>.Failure(new OpenInvitationTokenError.NotFound());

        if (invitation.IsConsumed)
            return Result<Response, OpenInvitationTokenError>.Failure(new OpenInvitationTokenError.AlreadyConsumed());

        if (invitation.IsExpired(clock.UtcNow))
            return Result<Response, OpenInvitationTokenError>.Failure(new OpenInvitationTokenError.Expired());

        try
        {
            invitation.MarkConsumed(clock.UtcNow, invitation.CreatedBy);
        }
        catch (InvitationAlreadyConsumedException)
        {
            return Result<Response, OpenInvitationTokenError>.Failure(new OpenInvitationTokenError.AlreadyConsumed());
        }
        catch (InvitationExpiredException)
        {
            return Result<Response, OpenInvitationTokenError>.Failure(new OpenInvitationTokenError.Expired());
        }

        await repo.UpdateAsync(invitation, ct);
        await repo.SaveChangesAsync(ct);

        return Result<Response, OpenInvitationTokenError>.Success(
            new Response(
                invitation.Id,
                invitation.TenantId,
                invitation.ProcedureInstanceId,
                invitation.ExpiresAt));
    }
}
