import { describe, expect, it } from "vitest";
import { describePasswordPolicy, validatePassword } from "./passwordPolicy.js";

const policy = {
  minLength: 8,
  requireUppercase: true,
  requireLowercase: true,
  requireDigit: true,
  requireSymbol: true,
};

describe("validatePassword", () => {
  it("accepts a password that meets all rules", () => {
    expect(validatePassword("Flit2026!", policy)).toBeNull();
  });

  it("rejects short passwords", () => {
    expect(validatePassword("Fl1!", policy)).toMatch(/8 caracteres/);
  });

  it("rejects passwords without uppercase", () => {
    expect(validatePassword("flit2026!", policy)).toMatch(/mayúscula/);
  });
});

describe("describePasswordPolicy", () => {
  it("lists all configured rules", () => {
    expect(describePasswordPolicy(policy)).toEqual([
      "Mínimo 8 caracteres",
      "Una mayúscula",
      "Una minúscula",
      "Un número",
      "Un símbolo",
    ]);
  });
});
