import { describe, expect, it } from "vitest";
import { invitationPreviewSchema } from "./onboarding.schemas.js";

describe("invitationPreviewSchema", () => {
  it("parses a valid preview response", () => {
    const parsed = invitationPreviewSchema.parse({
      valid: true,
      email: "nuevo.usuario@test.com",
      tenantId: "01930101-0001-7001-8001-000000000001",
      expiresAt: "2026-06-13T11:36:39-05:00",
      passwordPolicy: {
        minLength: 8,
        requireUppercase: true,
        requireLowercase: true,
        requireDigit: true,
        requireSymbol: true,
      },
    });

    expect(parsed.email).toBe("nuevo.usuario@test.com");
    expect(parsed.passwordPolicy.minLength).toBe(8);
  });
});
