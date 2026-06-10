namespace Flit.Modules.IdentityVerification.Integration;

/// <summary>
/// Contrato de integracion flit.procedures/TRAMITE_CREATED (Feature #9469 IDS-02).
/// </summary>
public sealed record TramiteCreatedIntegrationEvent(
    Guid TenantId,
    Guid ProcedureInstanceId,
    Guid ProcedureTypeId,
    string IdempotencyKey,
    IReadOnlyList<TramiteParticipantInfo> Participants,
    Guid ActorUserId);

public sealed record TramiteParticipantInfo(
    Guid? ProcedureActorId,
    string ParticipantRole,
    string Email);
