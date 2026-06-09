namespace Flit.Modules.Procedures.Domain;

/// <summary>Entrada append-only de <c>procedures.procedure_state_history</c> (TRA-01 #9433).</summary>
public sealed record ProcedureStateHistoryEntry(
    Guid Id,
    Guid TenantId,
    Guid ProcedureInstanceId,
    string? FromState,
    string ToState,
    string? Reason,
    Guid ChangedBy,
    DateTimeOffset ChangedAt);
