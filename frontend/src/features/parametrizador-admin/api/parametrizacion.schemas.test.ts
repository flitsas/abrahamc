import { describe, expect, it } from "vitest";
import {
  catalogFamiliesResponseSchema,
  createProcedureTypeRequestSchema,
  procedureMatrixSchema,
  simulateRulesResponseSchema,
} from "./parametrizacion.schemas.js";
import {
  countActiveDataEdges,
  validateMinActiveDataEdges,
} from "../lib/pipelineValidation.js";

describe("parametrizacion.schemas", () => {
  it("parses catalog families response", () => {
    const parsed = catalogFamiliesResponseSchema.parse({
      items: [
        {
          id: "01930101-0001-7001-8001-000000000001",
          code: "TRANSFERENCIA",
          name: "Transferencias",
          displayOrder: 10,
        },
      ],
    });
    expect(parsed.items).toHaveLength(1);
    expect(parsed.items[0].code).toBe("TRANSFERENCIA");
  });

  it("parses procedure matrix with edges", () => {
    const parsed = procedureMatrixSchema.parse({
      procedureTypeId: "01930101-0001-7001-8001-000000000002",
      code: "TRF-01",
      name: "Transferencia vehicular",
      familyCode: "TRANSFERENCIA",
      maxSteps: 4,
      globalIsActive: true,
      tenantActivationActive: true,
      edges: [
        {
          code: "propietario",
          name: "Propietario",
          edgeKind: "data",
          isActive: true,
          isRequired: true,
          displayOrder: 1,
          roleLabel: "propietario",
        },
      ],
      sections: [],
      requiredDocuments: [],
    });
    expect(parsed.edges[0].code).toBe("propietario");
  });

  it("validates create procedure type request", () => {
    const parsed = createProcedureTypeRequestSchema.parse({
      tenantId: "01930101-0001-7001-8001-000000000001",
      familyCode: "TRANSFERENCIA",
      code: "TRF-01",
      slug: "trf-01",
      name: "Transferencia",
      maxSteps: 4,
      edges: [
        {
          edgeCode: "propietario",
          isActive: true,
          isRequired: true,
          displayOrder: 1,
        },
      ],
    });
    expect(parsed.maxSteps).toBe(4);
  });

  it("parses simulate rules response", () => {
    const parsed = simulateRulesResponseSchema.parse({
      matchedRules: [
        {
          ruleId: "01930101-0001-7001-8001-000000000003",
          ruleName: "Regla placa",
          priority: 100,
          actions: [{ type: "require_field", params: { field: "placa" } }],
        },
      ],
      actions: [{ type: "require_field", params: { field: "placa" } }],
      conflicts: [],
    });
    expect(parsed.matchedRules).toHaveLength(1);
  });
});

describe("pipelineValidation", () => {
  it("counts active non-document edges", () => {
    const count = countActiveDataEdges([
      { edgeCode: "propietario", isActive: true },
      { edgeCode: "comprador", isActive: true },
      { edgeCode: "vehiculo", isActive: true },
      { edgeCode: "locatario", isActive: true },
      { edgeCode: "documentos", isActive: true },
    ]);
    expect(count).toBe(4);
  });

  it("rejects pipeline with fewer than 4 active data edges", () => {
    const result = validateMinActiveDataEdges([
      { edgeCode: "propietario", isActive: true },
      { edgeCode: "comprador", isActive: true },
      { edgeCode: "documentos", isActive: true },
    ]);
    expect(result.isValid).toBe(false);
    expect(result.message).toContain("al menos 4");
  });
});
