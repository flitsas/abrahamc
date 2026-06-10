import { describe, expect, it } from "vitest";
import {
  collaboratorSchema,
  collaboratorsPageSchema,
} from "./admin.schemas.js";

describe("admin.schemas", () => {
  it("parses collaborators page", () => {
    const parsed = collaboratorsPageSchema.parse({
      items: [
        {
          id: "01930201-0001-7001-8001-000000000011",
          tenantId: "01930101-0001-7001-8001-000000000001",
          email: "admin@transportes-andina.com",
          accountState: "active",
          fullName: "Admin Andina",
          phone: null,
          roleSlugs: ["tenant-admin"],
          rowVersion: 1,
        },
      ],
      total: 1,
      page: 1,
      limit: 10,
    });

    expect(parsed.items).toHaveLength(1);
    expect(collaboratorSchema.parse(parsed.items[0]).email).toBe(
      "admin@transportes-andina.com",
    );
  });

  it("parses collaborators without profile fields when backend omits nulls", () => {
    const parsed = collaboratorsPageSchema.parse({
      items: [
        {
          id: "01930201-0001-7001-8001-000000000099",
          tenantId: "01930101-0001-7001-8001-000000000001",
          email: "pending@transportes-andina.com",
          accountState: "pending",
          roleSlugs: [],
          rowVersion: 1,
        },
      ],
      total: 1,
      page: 1,
      limit: 10,
    });

    expect(parsed.items[0].fullName).toBeUndefined();
    expect(parsed.items[0].phone).toBeUndefined();
  });
});
