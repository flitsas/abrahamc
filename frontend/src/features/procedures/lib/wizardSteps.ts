import type {
  FormSectionConfig,
  ProcedureConfiguration,
  ProcedureEdgeConfig,
} from "../api/procedure-wizard.schemas.js";

export type WizardStep = {
  edge: ProcedureEdgeConfig;
  sections: FormSectionConfig[];
  isCopropiedad: boolean;
};

export function buildWizardSteps(config: ProcedureConfiguration): WizardStep[] {
  const activeEdges = [...config.edges]
    .filter((edge) => edge.isActive)
    .sort((a, b) => a.displayOrder - b.displayOrder);

  return activeEdges.map((edge) => {
    const sections = config.sections
      .filter(
        (section) =>
          section.edgeCode === edge.code ||
          (!section.edgeCode && edge.edgeKind !== "documents"),
      )
      .sort((a, b) => a.displayOrder - b.displayOrder);

    const isCopropiedad =
      edge.code === "copropiedad" ||
      sections.some(
        (section) =>
          /copropiedad/i.test(section.sectionKey) ||
          /copropiedad/i.test(section.title),
      );

    return { edge, sections, isCopropiedad };
  });
}

export function stepHasPlateTrigger(sections: FormSectionConfig[]): boolean {
  return sections.some((section) =>
    section.fields.some(
      (field) =>
        field.isTrigger || field.fieldKey.toLowerCase().includes("placa"),
    ),
  );
}

export function isPlateField(fieldKey: string, isTrigger: boolean): boolean {
  return isTrigger || fieldKey.toLowerCase().includes("placa");
}
