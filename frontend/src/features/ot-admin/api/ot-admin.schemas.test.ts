import { describe, expect, it } from "vitest";
import {
  otConsolidatedOrderSchema,
  otDashboardSchema,
  otIntegrationModeSchema,
} from "./ot-admin.schemas.js";

describe("ot-admin.schemas", () => {
  it("parses dashboard response with snake_case fields", () => {
    const parsed = otDashboardSchema.parse({
      traffic_agency_id: "01930101-0001-7001-8001-000000000001",
      integration_mode: "dashboard",
      total_procedures: 12,
      by_state: [
        { state: "pendiente", count: 5 },
        { state: "aprobado", count: 7 },
      ],
      recent_procedures: [
        {
          id: "01930101-0001-7001-8001-000000000002",
          reference_number: "TRF-2026-001",
          state: "pendiente",
          radicated_at: "2026-06-12T10:00:00Z",
        },
      ],
    });

    expect(parsed.total_procedures).toBe(12);
    expect(parsed.recent_procedures[0].reference_number).toBe("TRF-2026-001");
  });

  it("parses consolidated order items", () => {
    const parsed = otConsolidatedOrderSchema.parse({
      orderId: "01930101-0001-7001-8001-000000000003",
      version: 2,
      items: [
        {
          id: "01930101-0001-7001-8001-000000000004",
          position: 1,
          source: "catalog",
          procedureDocumentCatalogId: "01930101-0001-7001-8001-000000000005",
          catalogCode: "SOAT",
          catalogName: "SOAT vigente",
          customLabel: null,
        },
      ],
    });

    expect(parsed.items).toHaveLength(1);
    expect(parsed.items[0].catalogCode).toBe("SOAT");
  });

  it("accepts quipux integration mode", () => {
    expect(otIntegrationModeSchema.parse("quipux")).toBe("quipux");
  });
});
