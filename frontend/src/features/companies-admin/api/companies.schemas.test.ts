import { describe, expect, it } from "vitest";
import {
  companiesPageSchema,
  companyRowSchema,
  moduleConfigSchema,
} from "./companies.schemas.js";

describe("companies.schemas", () => {
  it("parses companies page response", () => {
    const parsed = companiesPageSchema.parse({
      data: [
        {
          id: "01930110-0001-7001-8001-000000000001",
          tenantId: "01930101-0001-7001-8001-000000000001",
          tenantName: "Transportes Andina",
          nit: "900111222-3",
          legalName: "Transportes Andina S.A.",
          commercialName: "Andina",
          status: "active",
          modulesEnabledJson: '{"registration":true}',
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-02T00:00:00Z",
          actions: { canView: true, canEdit: true },
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    });

    expect(parsed.data).toHaveLength(1);
    expect(companyRowSchema.parse(parsed.data[0]).nit).toBe("900111222-3");
  });

  it("parses module config dto", () => {
    const parsed = moduleConfigSchema.parse({
      id: "01930120-0001-7001-8001-000000000001",
      moduleKey: "registration",
      configJson: '{"default_ot_code":"OT-BOGOTA"}',
      isActive: true,
      version: 1,
      updatedAt: "2026-01-01T00:00:00Z",
    });

    expect(parsed.moduleKey).toBe("registration");
  });
});
