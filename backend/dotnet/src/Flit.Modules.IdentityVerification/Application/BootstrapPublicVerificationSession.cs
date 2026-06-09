using System.Text;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9481 — GET /public/idsecure/session: valida token y bootstrap sesion movil.
/// </summary>
public static class BootstrapPublicVerificationSession
{
    public const string ExpiredResendMessage =
        "El enlace de validación expiró. Solicite un nuevo correo de invitación al operador del trámite.";

    public sealed record Command(string PlainToken);

    public sealed record Response(
        Guid VerificationSessionId,
        short CurrentStep,
        Guid InvitationId);

    public abstract record BootstrapSessionError(string Code, string Message, int HttpStatus)
    {
        public sealed record NotFound()
            : BootstrapSessionError("TOKEN_NOT_FOUND", "Token invalido", 404);

        public sealed record Expired()
            : BootstrapSessionError("TOKEN_EXPIRED", ExpiredResendMessage, 401);

        public sealed record AlreadyConsumedWithoutSession()
            : BootstrapSessionError(
                "TOKEN_CONSUMED",
                "Este enlace ya no está disponible. Solicite un nuevo correo al operador del trámite.",
                410);

        public sealed record CatalogNotConfigured(string Detail)
            : BootstrapSessionError("CATALOG_NOT_CONFIGURED", Detail, 503);
    }

    public static async Task<Result<Response, BootstrapSessionError>> HandleAsync(
        Command cmd,
        IVerificationInvitationRepository invitationRepo,
        IVerificationSessionRepository sessionRepo,
        IVerificationSessionStepRepository stepRepo,
        IIdSecureCatalogLookup catalogLookup,
        IInvitationTokenHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.PlainToken))
            return Result<Response, BootstrapSessionError>.Failure(new BootstrapSessionError.NotFound());

        var tokenHash = hasher.HashToken(Encoding.UTF8.GetBytes(cmd.PlainToken));
        var invitation = await invitationRepo.FindByTokenHashAsync(tokenHash, ct);
        if (invitation is null)
            return Result<Response, BootstrapSessionError>.Failure(new BootstrapSessionError.NotFound());

        if (invitation.IsExpired(clock.UtcNow))
            return Result<Response, BootstrapSessionError>.Failure(new BootstrapSessionError.Expired());

        var existingSession = await sessionRepo.FindByInvitationIdAsync(
            invitation.TenantId, invitation.Id, ct);

        if (existingSession is not null)
        {
            // Flujo terminado (4 pasos): no reabrir stepper ni devolver session id por el mismo enlace.
            if (existingSession.CurrentStep >= VerificationSession.CompletedStepCount)
            {
                return Result<Response, BootstrapSessionError>.Failure(
                    new BootstrapSessionError.AlreadyConsumedWithoutSession());
            }

            await EnsureDefaultStepsAsync(existingSession, stepRepo, clock, ct);

            return Result<Response, BootstrapSessionError>.Success(
                new Response(existingSession.Id, existingSession.CurrentStep, invitation.Id));
        }

        if (invitation.IsConsumed)
        {
            return Result<Response, BootstrapSessionError>.Failure(
                new BootstrapSessionError.AlreadyConsumedWithoutSession());
        }

        var documentTypeId = await catalogLookup.GetDocumentTypeIdByCodeAsync("CC", ct);
        if (documentTypeId is null)
        {
            return Result<Response, BootstrapSessionError>.Failure(
                new BootstrapSessionError.CatalogNotConfigured(
                    "Tipo de documento CC no encontrado en catalogs.document_types."));
        }

        var now = clock.UtcNow;
        invitation.MarkConsumed(now, invitation.CreatedBy);
        await invitationRepo.UpdateAsync(invitation, ct);

        var session = VerificationSession.BootstrapFromInvitation(
            invitation,
            documentTypeId.Value,
            now);

        await sessionRepo.AddAsync(session, ct);

        var defaultSteps = VerificationSessionStep.CreateDefaultSequence(session, now);
        await stepRepo.AddRangeAsync(defaultSteps, ct);
        // Un solo SaveChanges: invitación + sesión + pasos (atómico; evita sesiones huérfanas).
        await stepRepo.SaveChangesAsync(ct);

        return Result<Response, BootstrapSessionError>.Success(
            new Response(session.Id, session.CurrentStep, invitation.Id));
    }

    /// <summary>
    /// Repara sesiones creadas cuando el insert de pasos falló en un intento anterior.
    /// </summary>
    private static async Task EnsureDefaultStepsAsync(
        VerificationSession session,
        IVerificationSessionStepRepository stepRepo,
        IClock clock,
        CancellationToken ct)
    {
        var existingSteps = await stepRepo.GetBySessionIdAsync(session.TenantId, session.Id, ct);
        if (existingSteps.Count > 0)
            return;

        var now = clock.UtcNow;
        var defaultSteps = VerificationSessionStep.CreateDefaultSequence(session, now);
        await stepRepo.AddRangeAsync(defaultSteps, ct);
        await stepRepo.SaveChangesAsync(ct);
    }
}
