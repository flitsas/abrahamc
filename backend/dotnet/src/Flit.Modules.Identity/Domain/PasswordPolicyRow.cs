namespace Flit.Modules.Identity.Domain;

/// <summary>Política de lockout por tenant (identity.password_policies, HU #9416).</summary>
public sealed record PasswordPolicyRow(int LockoutThreshold, int LockoutMinutes);
