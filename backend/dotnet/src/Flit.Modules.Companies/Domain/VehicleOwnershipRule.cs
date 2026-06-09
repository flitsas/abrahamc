namespace Flit.Modules.Companies.Domain;

public static class VehicleOwnershipRuleType
{
    public const string Allow = "allow";
    public const string Block = "block";
    public const string Warn = "warn";
    public const string RequireException = "require_exception";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Allow, Block, Warn, RequireException,
    };
}

public sealed class VehicleOwnershipRule
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string RuleType { get; private set; } = VehicleOwnershipRuleType.Allow;
    public string ConditionJson { get; private set; } = "{}";
    public int Priority { get; private set; } = 100;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public int RowVersion { get; private set; }

    private VehicleOwnershipRule() { }

    public static VehicleOwnershipRule Create(
        Guid tenantId,
        string name,
        string ruleType,
        string conditionJson,
        int priority,
        Guid actorUserId,
        DateTimeOffset now,
        bool isActive = true)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name requerido", nameof(name));
        if (!VehicleOwnershipRuleType.All.Contains(ruleType))
            throw new ArgumentException($"RuleType inválido: {ruleType}", nameof(ruleType));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        return new VehicleOwnershipRule
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            RuleType = ruleType,
            ConditionJson = string.IsNullOrWhiteSpace(conditionJson) ? "{}" : conditionJson,
            Priority = priority,
            IsActive = isActive,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        };
    }

    public void Update(
        string name,
        string ruleType,
        string conditionJson,
        int priority,
        bool isActive,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name requerido", nameof(name));
        if (!VehicleOwnershipRuleType.All.Contains(ruleType))
            throw new ArgumentException($"RuleType inválido: {ruleType}", nameof(ruleType));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        Name = name.Trim();
        RuleType = ruleType;
        ConditionJson = string.IsNullOrWhiteSpace(conditionJson) ? "{}" : conditionJson;
        Priority = priority;
        IsActive = isActive;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    public static string ToExceptionCode(Guid ruleId)
        => $"VOR-{ruleId.ToString("N")[..12].ToUpperInvariant()}";
}
