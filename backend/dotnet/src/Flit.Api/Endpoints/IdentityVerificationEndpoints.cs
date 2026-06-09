using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Api.Endpoints;

/// <summary>HU TRA-03 #9435 — Validación de identidad reutilizable.</summary>
public static class IdentityVerificationEndpoints
{
    public static void MapIdentityVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity-verification")
            .WithTags("Identity Verification");

        group.MapPost("/sessions/start", async (
            StartIdentityVerificationRequest req,
            IVerificationSessionRepository sessionRepo,
            IProcedureIdentityValidationRepository linkRepo,
            IIdentityVerificationProvider provider,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });
            if (req.InitiatedByUserId == Guid.Empty)
                return Results.BadRequest(new { error = "initiatedByUserId es requerido." });
            if (string.IsNullOrWhiteSpace(req.DocumentTypeCode))
                return Results.BadRequest(new { error = "documentTypeCode es requerido." });
            if (string.IsNullOrWhiteSpace(req.DocumentNumber))
                return Results.BadRequest(new { error = "documentNumber es requerido." });

            var result = await StartActorIdentityVerification.HandleAsync(
                new StartActorIdentityVerification.Command(
                    req.TenantId,
                    req.InitiatedByUserId,
                    req.DocumentTypeCode,
                    req.DocumentNumber,
                    req.ProcedureInstanceId,
                    req.ActorId),
                sessionRepo,
                linkRepo,
                provider,
                ct);

            return Results.Ok(new IdentityVerificationResponse(
                result.SessionId,
                result.Status,
                result.Provider,
                result.Reused,
                result.LivenessPerformed,
                result.ChannelsCompleted,
                result.ProcedureLinkId));
        })
        .WithName("StartIdentityVerification")
        .WithSummary("Inicia o reutiliza sesión de validación de identidad (TRA-03)");

        group.MapGet("/sessions/{id:guid}", async (
            Guid id,
            Guid tenantId,
            IVerificationSessionRepository sessionRepo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId es requerido." });

            var session = await sessionRepo.GetByIdAsync(tenantId, id, ct);
            return session is null
                ? Results.NotFound(new { error = $"Sesión '{id}' no encontrada." })
                : Results.Ok(new IdentityVerificationSessionDetail(
                    session.Id,
                    session.Status,
                    session.Provider,
                    session.Verdict,
                    session.Score,
                    session.LivenessPerformed,
                    session.ChannelsCompleted,
                    session.PerformedAt,
                    session.ExpiresAt));
        })
        .WithName("GetIdentityVerificationSession")
        .WithSummary("Consulta una sesión de validación de identidad");
    }

    public sealed record StartIdentityVerificationRequest(
        Guid TenantId,
        Guid InitiatedByUserId,
        string DocumentTypeCode,
        string DocumentNumber,
        Guid? ProcedureInstanceId,
        Guid? ActorId);

    public sealed record IdentityVerificationResponse(
        Guid SessionId,
        string Status,
        string Provider,
        bool Reused,
        bool LivenessPerformed,
        IReadOnlyList<string> ChannelsCompleted,
        Guid? ProcedureLinkId);

    public sealed record IdentityVerificationSessionDetail(
        Guid SessionId,
        string Status,
        string Provider,
        string? Verdict,
        decimal? Score,
        bool LivenessPerformed,
        IReadOnlyList<string> ChannelsCompleted,
        DateTimeOffset? PerformedAt,
        DateTimeOffset? ExpiresAt);
}
