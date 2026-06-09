using Flit.SharedKernel;

namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Ítem ordenado del consolidado OT (ot.ot_consolidated_doc_order_items — Feature #9379).
/// </summary>
public sealed class OtConsolidatedDocOrderItem
{
    public static class Sources
    {
        public const string Global = "global";
        public const string Custom = "custom";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Global, Custom };
    }

    public Guid Id { get; private set; }
    public Guid TrafficAgencyId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? ProcedureDocumentCatalogId { get; private set; }
    public string? CustomLabel { get; private set; }
    public int Position { get; private set; }
    public string Source { get; private set; } = Sources.Global;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public int RowVersion { get; private set; }

    private OtConsolidatedDocOrderItem() { }

    public static Result<OtConsolidatedDocOrderItem, string> CreateGlobal(
        Guid trafficAgencyId,
        Guid orderId,
        Guid procedureDocumentCatalogId,
        int position,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (procedureDocumentCatalogId == Guid.Empty)
            return Result<OtConsolidatedDocOrderItem, string>.Failure("procedure_document_catalog_id es requerido.");

        if (position < 1)
            return Result<OtConsolidatedDocOrderItem, string>.Failure("position debe ser >= 1.");

        return Result<OtConsolidatedDocOrderItem, string>.Success(new OtConsolidatedDocOrderItem
        {
            Id = Guid.NewGuid(),
            TrafficAgencyId = trafficAgencyId,
            OrderId = orderId,
            ProcedureDocumentCatalogId = procedureDocumentCatalogId,
            CustomLabel = null,
            Position = position,
            Source = Sources.Global,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        });
    }

    public static Result<OtConsolidatedDocOrderItem, string> CreateCustom(
        Guid trafficAgencyId,
        Guid orderId,
        string customLabel,
        int position,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(customLabel))
            return Result<OtConsolidatedDocOrderItem, string>.Failure("custom_label es requerido.");

        if (position < 1)
            return Result<OtConsolidatedDocOrderItem, string>.Failure("position debe ser >= 1.");

        return Result<OtConsolidatedDocOrderItem, string>.Success(new OtConsolidatedDocOrderItem
        {
            Id = Guid.NewGuid(),
            TrafficAgencyId = trafficAgencyId,
            OrderId = orderId,
            ProcedureDocumentCatalogId = null,
            CustomLabel = customLabel.Trim(),
            Position = position,
            Source = Sources.Custom,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        });
    }

    public void SetPosition(int position, Guid actorUserId, DateTimeOffset now)
    {
        Position = position;
        UpdatedBy = actorUserId;
        UpdatedAt = now;
        RowVersion++;
    }
}
