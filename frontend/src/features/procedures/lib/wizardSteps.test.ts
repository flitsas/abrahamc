import { describe, expect, it } from "vitest";
import { buildWizardSteps, stepHasPlateTrigger } from "./wizardSteps.js";
import type { ProcedureConfiguration } from "../api/procedure-wizard.schemas.js";

const baseConfig: ProcedureConfiguration = {
  procedureTypeId: "01930101-0001-7001-8001-000000000001",
  code: "TRA_ESTANDAR",
  edges: [
    {
      code: "vehiculo",
      name: "Vehículo",
      edgeKind: "vehicle",
      isActive: true,
      isRequired: true,
      displayOrder: 1,
      roleLabel: null,
    },
    {
      code: "copropiedad",
      name: "Copropiedad",
      edgeKind: "person",
      isActive: true,
      isRequired: false,
      displayOrder: 2,
      roleLabel: null,
    },
  ],
  sections: [
    {
      sectionKey: "vehiculo_datos",
      title: "Datos del vehículo",
      displayOrder: 1,
      uiMode: "interactive",
      edgeCode: "vehiculo",
      fields: [
        {
          fieldKey: "placa",
          dataType: "text",
          label: "Placa",
          isRequired: true,
          displayOrder: 1,
          uiState: "lleno",
          isTrigger: true,
          validation: {},
          options: [],
        },
      ],
    },
    {
      sectionKey: "copropiedad_datos",
      title: "Copropiedad",
      displayOrder: 1,
      uiMode: "interactive",
      edgeCode: "copropiedad",
      fields: [],
    },
  ],
  queries: [],
  requiredDocuments: [],
  resolutionLayers: [],
};

describe("wizardSteps", () => {
  it("builds ordered wizard steps and flags copropiedad", () => {
    const steps = buildWizardSteps(baseConfig);

    expect(steps).toHaveLength(2);
    expect(steps[0].edge.code).toBe("vehiculo");
    expect(steps[1].isCopropiedad).toBe(true);
  });

  it("detects plate trigger fields", () => {
    const steps = buildWizardSteps(baseConfig);
    expect(stepHasPlateTrigger(steps[0].sections)).toBe(true);
    expect(stepHasPlateTrigger(steps[1].sections)).toBe(false);
  });
});
