import type { PasswordPolicy } from "../api/onboarding.schemas.js";

export function validatePassword(
  password: string,
  policy: PasswordPolicy,
): string | null {
  if (password.length < policy.minLength) {
    return `La contraseña debe tener al menos ${policy.minLength} caracteres.`;
  }
  if (policy.requireUppercase && !/[A-Z]/.test(password)) {
    return "Debe incluir al menos una letra mayúscula.";
  }
  if (policy.requireLowercase && !/[a-z]/.test(password)) {
    return "Debe incluir al menos una letra minúscula.";
  }
  if (policy.requireDigit && !/\d/.test(password)) {
    return "Debe incluir al menos un número.";
  }
  if (policy.requireSymbol && !/[^A-Za-z0-9]/.test(password)) {
    return "Debe incluir al menos un símbolo.";
  }
  return null;
}

export function describePasswordPolicy(policy: PasswordPolicy): string[] {
  const rules: string[] = [`Mínimo ${policy.minLength} caracteres`];
  if (policy.requireUppercase) rules.push("Una mayúscula");
  if (policy.requireLowercase) rules.push("Una minúscula");
  if (policy.requireDigit) rules.push("Un número");
  if (policy.requireSymbol) rules.push("Un símbolo");
  return rules;
}
