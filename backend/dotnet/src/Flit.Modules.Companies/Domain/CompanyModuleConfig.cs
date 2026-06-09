namespace Flit.Modules.Companies.Domain;

public sealed class CompanyModuleConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ModuleKey { get; private set; } = string.Empty;
    public string ConfigJson { get; private set; } = "{}";
    public bool IsActive { get; private set; } = true;
    public int Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public int RowVersion { get; private set; }

    private CompanyModuleConfig() { }

    public static CompanyModuleConfig Create(
        Guid tenantId,
        string moduleKey,
        string configJson,
        Guid actorUserId,
        DateTimeOffset now,
        bool isActive = true)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido", nameof(tenantId));
        if (!CompanyModuleKey.All.Contains(moduleKey))
            throw new ArgumentException($"ModuleKey inválido: {moduleKey}", nameof(moduleKey));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        return new CompanyModuleConfig
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ModuleKey = moduleKey,
            ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson,
            IsActive = isActive,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        };
    }

    /// <summary>Persiste cambios hot-reload (sin redeploy); incrementa version para trazabilidad.</summary>
    public void ApplyHotReload(
        string configJson,
        bool isActive,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (!CompanyModuleKey.All.Contains(ModuleKey))
            throw new InvalidOperationException($"ModuleKey inválido: {ModuleKey}");
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        IsActive = isActive;
        Version += 1;
        UpdatedBy = actorUserId;
        UpdatedAt = now;
    }
}
