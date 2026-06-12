import { describe, expect, it } from "vitest";
import {
  createProcedureInstanceResponseSchema,
  procedureInstanceDetailSchema,
  procedureInstancesPageSchema,
} from "./procedure-instances.schemas.js";
import { queryResultsResponseSchema } from "./procedure-wizard.schemas.js";

describe("procedure-instances.schemas", () => {
  it("parses paginated instances list with compositeId", () => {
    const parsed = procedureInstancesPageSchema.parse({
      items: [
        {
          id: "01930101-0001-7001-8001-000000000001",
          compositeId: "TRA_ESTANDAR-TRF-2026-001",
          referenceNumber: "TRF-2026-001",
          state: "borrador",
          procedureTypeCode: "TRA_ESTANDAR",
          radicatedAt: "2026-06-12T10:00:00Z",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });

    expect(parsed.items[0].compositeId).toBe("TRA_ESTANDAR-TRF-2026-001");
    expect(parsed.totalCount).toBe(1);
  });

  it("parses create instance response", () => {
    const parsed = createProcedureInstanceResponseSchema.parse({
      id: "01930101-0001-7001-8001-000000000002",
      tenantId: "01930101-0001-7001-8001-000000000003",
      procedureTypeId: "01930101-0001-7001-8001-000000000004",
      procedureTypeCode: "TRA_ESTANDAR",
      trafficAgencyId: null,
      referenceNumber: "TRF-2026-002",
      state: "borrador",
      configSchemaVersion: 1,
      createdAt: "2026-06-12T10:00:00Z",
      createdBy: "01930101-0001-7001-8001-000000000005",
    });

    expect(parsed.procedureTypeCode).toBe("TRA_ESTANDAR");
  });

  it("parses instance detail for wizard continue flow", () => {
    const parsed = procedureInstanceDetailSchema.parse({
      id: "01930101-0001-7001-8001-000000000006",
      tenantId: "01930101-0001-7001-8001-000000000003",
      procedureTypeId: "01930101-0001-7001-8001-000000000004",
      referenceNumber: "TRF-2026-003",
      state: "borrador",
      configSnapshot: JSON.stringify({ code: "TRA_ESTANDAR" }),
      configSchemaVersion: 1,
      radicatedAt: null,
    });

    expect(parsed.referenceNumber).toBe("TRF-2026-003");
  });
});

describe("procedure-wizard query schemas", () => {
  it("parses query results for non-blocking banners", () => {
    const parsed = queryResultsResponseSchema.parse({
      canContinue: true,
      results: [
        {
          connectorCode: "RUNT",
          status: "ok",
          succeeded: true,
          mandatory: true,
          circuitOpen: false,
        },
        {
          connectorCode: "SIMIT",
          status: "failed",
          succeeded: false,
          mandatory: false,
          circuitOpen: true,
        },
      ],
    });

    expect(parsed.canContinue).toBe(true);
    expect(parsed.results[1].circuitOpen).toBe(true);
  });
});
