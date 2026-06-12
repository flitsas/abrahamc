import { z } from "zod";

export const catalogFamilySchema = z.object({
  id: z.string().uuid(),
  code: z.string(),
  name: z.string(),
  displayOrder: z.number(),
});

export const catalogFamiliesResponseSchema = z.object({
  items: z.array(catalogFamilySchema),
});

export const createCatalogFamilyRequestSchema = z.object({
  code: z.string().min(1),
  name: z.string().min(1),
  displayOrder: z.number().int().optional(),
});

export const catalogEdgeSchema = z.object({
  code: z.string(),
  name: z.string(),
  edgeKind: z.string(),
  displayOrder: z.number(),
});

export const catalogEdgesResponseSchema = z.object({
  items: z.array(catalogEdgeSchema),
});

export const procedureTypeSummarySchema = z.object({
  id: z.string().uuid(),
  code: z.string(),
  name: z.string(),
  familyCode: z.string(),
  globalIsActive: z.boolean(),
  tenantActivationActive: z.boolean(),
  activeEdgeCount: z.number(),
  maxSteps: z.number(),
});

export const procedureTypesResponseSchema = z.object({
  items: z.array(procedureTypeSummarySchema),
});

export const procedureEdgeConfigSchema = z.object({
  code: z.string(),
  name: z.string(),
  edgeKind: z.string(),
  isActive: z.boolean(),
  isRequired: z.boolean(),
  displayOrder: z.number(),
  roleLabel: z.string().nullish(),
});

export const formFieldConfigSchema = z.object({
  fieldKey: z.string(),
  dataType: z.string(),
  label: z.string(),
  isRequired: z.boolean(),
  displayOrder: z.number(),
  uiState: z.string(),
  isTrigger: z.boolean(),
  validation: z.record(z.string(), z.unknown()).default({}),
  options: z.array(z.unknown()).default([]),
});

export const formSectionConfigSchema = z.object({
  sectionKey: z.string(),
  title: z.string(),
  displayOrder: z.number(),
  uiMode: z.string(),
  edgeCode: z.string().nullish(),
  fields: z.array(formFieldConfigSchema).default([]),
});

export const procedureMatrixSchema = z.object({
  procedureTypeId: z.string().uuid(),
  code: z.string(),
  name: z.string(),
  familyCode: z.string(),
  maxSteps: z.number(),
  globalIsActive: z.boolean(),
  tenantActivationActive: z.boolean(),
  edges: z.array(procedureEdgeConfigSchema),
  sections: z.array(formSectionConfigSchema).default([]),
  requiredDocuments: z.array(z.unknown()).default([]),
});

export const adminFormFieldItemSchema = z.object({
  id: z.string().uuid(),
  fieldKey: z.string(),
  dataType: z.string(),
  label: z.string(),
  isRequired: z.boolean(),
  displayOrder: z.number(),
  uiState: z.string(),
  isTrigger: z.boolean(),
  validation: z.record(z.string(), z.unknown()).default({}),
  options: z.array(z.unknown()).default([]),
  rowVersion: z.number(),
});

export const adminFormSectionItemSchema = z.object({
  id: z.string().uuid(),
  sectionKey: z.string(),
  title: z.string(),
  displayOrder: z.number(),
  uiMode: z.string(),
  edgeCode: z.string(),
  rowVersion: z.number(),
  fields: z.array(adminFormFieldItemSchema).default([]),
});

export const adminEdgeFormSchema = z.object({
  procedureTypeCode: z.string(),
  edgeCode: z.string(),
  sections: z.array(adminFormSectionItemSchema).default([]),
});

export const createProcedureTypeEdgeInputSchema = z.object({
  edgeCode: z.string(),
  isActive: z.boolean(),
  isRequired: z.boolean(),
  displayOrder: z.number(),
  roleLabel: z.string().nullish(),
});

export const createProcedureTypeRequestSchema = z.object({
  tenantId: z.string().uuid(),
  trafficAgencyId: z.string().uuid().nullish(),
  familyCode: z.string(),
  code: z.string(),
  slug: z.string(),
  name: z.string(),
  maxSteps: z.number().int().min(1).max(4),
  edges: z.array(createProcedureTypeEdgeInputSchema).min(1),
});

export const createProcedureTypeResponseSchema = z.object({
  procedureTypeId: z.string().uuid(),
  code: z.string(),
  slug: z.string(),
  name: z.string(),
});

export const createFormSectionRequestSchema = z.object({
  sectionKey: z.string().min(1),
  title: z.string().min(1),
  displayOrder: z.number().int(),
  uiMode: z.enum(["read_only", "interactive"]),
});

export const createFormFieldRequestSchema = z.object({
  fieldKey: z.string().min(1),
  dataType: z.enum(["text", "select", "date"]),
  label: z.string().min(1),
  isRequired: z.boolean(),
  displayOrder: z.number().int(),
  uiState: z.string().default("visible"),
  isTrigger: z.boolean().default(false),
  validationJson: z.string().nullish(),
  optionsJson: z.string().nullish(),
});

export const ruleCatalogItemSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string().nullish(),
  conditionTree: z.unknown(),
  actions: z.unknown(),
  priority: z.number(),
  isActive: z.boolean(),
  rowVersion: z.number(),
});

export const ruleCatalogListSchema = z.array(ruleCatalogItemSchema);

export const ruleActionSchema = z.object({
  type: z.string(),
  params: z.unknown().optional(),
});

export const matchedRuleSchema = z.object({
  ruleId: z.string().uuid(),
  ruleName: z.string(),
  priority: z.number(),
  actions: z.array(ruleActionSchema).default([]),
});

export const ruleConflictSchema = z.object({
  conflictType: z.string(),
  target: z.string(),
  actionTypes: z.array(z.string()).default([]),
  ruleIds: z.array(z.string().uuid()).default([]),
});

export const simulateRulesResponseSchema = z.object({
  matchedRules: z.array(matchedRuleSchema).default([]),
  actions: z.array(ruleActionSchema).default([]),
  conflicts: z.array(ruleConflictSchema).default([]),
});

export const simulateRulesRequestSchema = z.object({
  tenantId: z.string().uuid(),
  procedureTypeId: z.string().uuid(),
  capturedFields: z.record(z.string(), z.string().nullable()),
  configSnapshot: z.unknown().optional(),
});

export type CatalogFamily = z.infer<typeof catalogFamilySchema>;
export type CatalogEdge = z.infer<typeof catalogEdgeSchema>;
export type ProcedureTypeSummary = z.infer<typeof procedureTypeSummarySchema>;
export type ProcedureMatrix = z.infer<typeof procedureMatrixSchema>;
export type ProcedureEdgeConfig = z.infer<typeof procedureEdgeConfigSchema>;
export type AdminEdgeForm = z.infer<typeof adminEdgeFormSchema>;
export type AdminFormSectionItem = z.infer<typeof adminFormSectionItemSchema>;
export type AdminFormFieldItem = z.infer<typeof adminFormFieldItemSchema>;
export type CreateProcedureTypeRequest = z.infer<
  typeof createProcedureTypeRequestSchema
>;
export type CreateFormSectionRequest = z.infer<
  typeof createFormSectionRequestSchema
>;
export type CreateFormFieldRequest = z.infer<
  typeof createFormFieldRequestSchema
>;
export type RuleCatalogItem = z.infer<typeof ruleCatalogItemSchema>;
export type SimulateRulesRequest = z.infer<typeof simulateRulesRequestSchema>;
export type SimulateRulesResponse = z.infer<typeof simulateRulesResponseSchema>;
