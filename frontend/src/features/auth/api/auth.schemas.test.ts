import { describe, expect, it } from "vitest";
import { loginResponseSchema } from "./auth.schemas.js";

describe("loginResponseSchema", () => {
  it("parses a valid login response", () => {
    const parsed = loginResponseSchema.parse({
      user: {
        userId: "01930201-0001-7001-8001-000000000010",
        email: "superadmin@flit.com.co",
        tenantId: "01930101-0001-7001-8001-000000000001",
        accountState: "active",
        isSuperAdmin: true,
        permissionSlugs: ["modulo.tramites.ver"],
        expiresInSeconds: 900,
      },
      permissionSlugs: ["modulo.tramites.ver"],
      expiresIn: 900,
    });

    expect(parsed.user.email).toBe("superadmin@flit.com.co");
    expect(parsed.permissionSlugs).toContain("modulo.tramites.ver");
  });
});
