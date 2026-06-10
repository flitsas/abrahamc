namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Maestro de compañía B2B (schema companies.companies). Relación 1:1 con identity.tenants.
/// </summary>
public sealed class Company
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nit { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string? CommercialName { get; private set; }
    public string ModulesEnabledJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public int RowVersion { get; private set; }

    private Company() { }

    public static Company Create(
        Guid tenantId,
        string nit,
        string legalName,
        string? commercialName,
        string modulesEnabledJson,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(nit))
            throw new ArgumentException("NIT requerido", nameof(nit));
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("LegalName requerido", nameof(legalName));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        return new Company
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Nit = nit.Trim(),
            LegalName = legalName.Trim(),
            CommercialName = string.IsNullOrWhiteSpace(commercialName) ? null : commercialName.Trim(),
            ModulesEnabledJson = string.IsNullOrWhiteSpace(modulesEnabledJson) ? "{}" : modulesEnabledJson,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
            RowVersion = 1,
        };
    }

    public void UpdateIndexProfile(
        string legalName,
        string? commercialName,
        string modulesEnabledJson,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("LegalName requerido", nameof(legalName));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("ActorUserId requerido", nameof(actorUserId));

        LegalName = legalName.Trim();
        CommercialName = string.IsNullOrWhiteSpace(commercialName) ? null : commercialName.Trim();
        ModulesEnabledJson = string.IsNullOrWhiteSpace(modulesEnabledJson) ? "{}" : modulesEnabledJson;
        UpdatedBy = actorUserId;
        UpdatedAt = now;
    }
}
