import { z } from "zod";

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

export const procedureConfigurationSchema = z.object({
  procedureTypeId: z.string().uuid(),
  code: z.string(),
  scope: z.enum(["global", "company", "ot"]).optional(),
  resolutionLayers: z.array(z.enum(["global", "company", "ot"])).default([]),
  edges: z.array(procedureEdgeConfigSchema),
  sections: z.array(formSectionConfigSchema).default([]),
  queries: z.array(z.unknown()).default([]),
  requiredDocuments: z.array(z.unknown()).default([]),
});

export const queryResultItemSchema = z.object({
  connectorCode: z.string(),
  status: z.string(),
  succeeded: z.boolean(),
  mandatory: z.boolean(),
  circuitOpen: z.boolean(),
  edgeRole: z.string().nullish(),
  integrationCallId: z.string().uuid().nullish(),
  snapshotId: z.string().uuid().nullish(),
});

export const queryResultsResponseSchema = z.object({
  procedureInstanceId: z.string().uuid().optional(),
  canContinue: z.boolean(),
  results: z.array(queryResultItemSchema),
});

export const runAsyncQueriesResponseSchema = z.object({
  jobId: z.string(),
  status: z.string(),
});

export const procedureOwnerSchema = z.object({
  documentTypeCode: z.string().min(1),
  documentNumber: z.string().min(1),
  fullName: z.string().min(1),
  ownershipPercentage: z.number().min(0).max(100),
});

export const saveOwnersRequestSchema = z.object({
  tenantId: z.string().uuid(),
  owners: z.array(procedureOwnerSchema).min(1),
});

export type SaveOwnersRequest = z.infer<typeof saveOwnersRequestSchema>;

export const ownershipValidateResponseSchema = z.object({
  ownershipValid: z.boolean(),
  totalPercentage: z.number(),
});

export type ProcedureConfiguration = z.infer<
  typeof procedureConfigurationSchema
>;
export type ProcedureEdgeConfig = z.infer<typeof procedureEdgeConfigSchema>;
export type FormFieldConfig = z.infer<typeof formFieldConfigSchema>;
export type FormSectionConfig = z.infer<typeof formSectionConfigSchema>;
export type QueryResultItem = z.infer<typeof queryResultItemSchema>;
export type QueryResultsResponse = z.infer<typeof queryResultsResponseSchema>;
export type ProcedureOwner = z.infer<typeof procedureOwnerSchema>;
export type OwnershipValidateResponse = z.infer<
  typeof ownershipValidateResponseSchema
>;
