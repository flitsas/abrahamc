namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Orden activo del consolidado por OT (#9379 / DOC-02 #9443).</summary>
public sealed record OtConsolidatedDocOrderRecord(
    Guid Id,
    Guid TrafficAgencyId,
    int Version,
    bool IsActive);

public sealed record OtConsolidatedDocOrderItemRecord(
    Guid Id,
    Guid OrderId,
    Guid TrafficAgencyId,
    Guid? DocumentTypeId,
    string? CustomLabel,
    int Position,
    string Source);
