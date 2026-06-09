using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU TRA-03 #9435 — Inicia o reutiliza validación de identidad para un actor.
/// AC1: SMS, email, OTP y liveness persistidos en verification_sessions.
/// AC2: Sesión passed vigente se reutiliza sin repetir liveness.
/// </summary>
public static class StartActorIdentityVerification
{
    public sealed record Command(
        Guid TenantId,
        Guid InitiatedByUserId,
        string DocumentTypeCode,
        string DocumentNumber,
        Guid? ProcedureInstanceId = null,
        Guid? ActorId = null);

    public sealed record Result(
        Guid SessionId,
        string Status,
        string Provider,
        bool Reused,
        bool LivenessPerformed,
        IReadOnlyList<string> ChannelsCompleted,
        Guid? ProcedureLinkId);

    public static async Task<Result> HandleAsync(
        Command command,
        IVerificationSessionRepository sessionRepo,
        IProcedureIdentityValidationRepository linkRepo,
        IIdentityVerificationProvider provider,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var docNumber = command.DocumentNumber.Trim();
        var docType = command.DocumentTypeCode.Trim().ToUpperInvariant();

        var reusable = await sessionRepo.FindReusablePassedSessionAsync(
            command.TenantId, docType, docNumber, now, ct);

        VerificationSession session;
        var reused = false;

        if (reusable is not null)
        {
            session = reusable;
            reused = true;
        }
        else
        {
            var typeId = IdentityDocumentTypes.ResolveTypeId(docType);
            session = VerificationSession.StartForActor(
                command.TenantId,
                command.InitiatedByUserId,
                docType,
                typeId,
                docNumber,
                provider.ProviderName,
                now);

            await sessionRepo.AddAsync(session, ct);

            var providerResult = await provider.RunChannelsAsync(VerificationChannels.All, ct);

            session.ApplyProviderResult(
                providerResult.Status,
                providerResult.Verdict,
                providerResult.Score,
                now,
                now.AddDays(90),
                providerResult.LivenessPerformed,
                providerResult.ChannelsCompleted,
                now,
                command.InitiatedByUserId);

            await sessionRepo.UpdateAsync(session, ct);
            await sessionRepo.SaveChangesAsync(ct);
        }

        Guid? linkId = null;
        if (command.ProcedureInstanceId is Guid instanceId)
        {
            var link = ProcedureIdentityValidation.LinkForActor(
                command.TenantId,
                instanceId,
                session.Id,
                command.ActorId,
                session.Verdict,
                now,
                command.InitiatedByUserId);

            await linkRepo.SaveAsync(link, ct);
            linkId = link.Id;
        }

        return new Result(
            session.Id,
            session.Status,
            session.Provider,
            reused,
            session.LivenessPerformed,
            session.ChannelsCompleted,
            linkId);
    }
}
