namespace Flit.Modules.Identity.Domain;

/// <summary>Política de complejidad por tenant (identity.password_policies, HU #9418).</summary>
public sealed record PasswordComplexityPolicyRow(
    int MinLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireSymbol,
    int HistoryCount,
    int MaxAgeDays,
    int LockoutThreshold,
    int LockoutMinutes)
{
    public static readonly PasswordComplexityPolicyRow Default = new(
        MinLength: 12,
        RequireUppercase: true,
        RequireLowercase: true,
        RequireDigit: true,
        RequireSymbol: true,
        HistoryCount: 5,
        MaxAgeDays: 90,
        LockoutThreshold: 5,
        LockoutMinutes: 15);
}
