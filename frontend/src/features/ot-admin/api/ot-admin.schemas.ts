import { z } from "zod";

export const otIntegrationModeSchema = z.enum(["dashboard", "quipux"]);

export const otAgencySchema = z.object({
  id: z.string().uuid(),
  code: z.string(),
  name: z.string(),
  city: z.string().nullish(),
  department: z.string().nullish(),
  mode: z.string(),
  isActive: z.boolean(),
});

export const otAgenciesPageSchema = z.object({
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
  items: z.array(otAgencySchema),
});

export const otDashboardStateCountSchema = z.object({
  state: z.string(),
  count: z.number(),
});

export const otRecentProcedureSchema = z.object({
  id: z.string().uuid(),
  reference_number: z.string(),
  state: z.string(),
  radicated_at: z.string().nullish(),
});

export const otDashboardSchema = z.object({
  traffic_agency_id: z.string().uuid(),
  integration_mode: otIntegrationModeSchema,
  total_procedures: z.number(),
  by_state: z.array(otDashboardStateCountSchema),
  recent_procedures: z.array(otRecentProcedureSchema),
});

export const otIntegrationModeResponseSchema = z.object({
  traffic_agency_id: z.string().uuid(),
  mode: otIntegrationModeSchema,
});

export const otConsolidatedOrderItemSchema = z.object({
  id: z.string().uuid(),
  position: z.number(),
  source: z.string(),
  procedureDocumentCatalogId: z.string().uuid().nullish(),
  catalogCode: z.string().nullish(),
  catalogName: z.string().nullish(),
  customLabel: z.string().nullish(),
});

export const otConsolidatedOrderSchema = z.object({
  orderId: z.string().uuid(),
  version: z.number(),
  items: z.array(otConsolidatedOrderItemSchema),
});

export const otSaveConsolidatedOrderItemSchema = z.object({
  id: z.string().uuid().nullish(),
  position: z.number().int().min(1),
  source: z.string().min(1),
  procedureDocumentCatalogId: z.string().uuid().nullish(),
  customLabel: z.string().nullish(),
});

export const otSaveConsolidatedOrderRequestSchema = z.object({
  items: z.array(otSaveConsolidatedOrderItemSchema),
});

export const otRuleSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  triggerEvent: z.string(),
  conditionTree: z.unknown(),
  actions: z.unknown(),
  priority: z.number(),
  isActive: z.boolean(),
});

export const otRulesListSchema = z.array(otRuleSchema);

export const integrationLogItemSchema = z.object({
  id: z.string().uuid(),
  direction: z.string(),
  eventType: z.string(),
  status: z.string(),
  receivedAt: z.string(),
  processedAt: z.string().nullish(),
  idempotencyKey: z.string().nullish(),
});

export const integrationLogsPageSchema = z.object({
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
  items: z.array(integrationLogItemSchema),
});

export type OtIntegrationMode = z.infer<typeof otIntegrationModeSchema>;
export type OtAgency = z.infer<typeof otAgencySchema>;
export type OtDashboard = z.infer<typeof otDashboardSchema>;
export type OtRecentProcedure = z.infer<typeof otRecentProcedureSchema>;
export type OtConsolidatedOrder = z.infer<typeof otConsolidatedOrderSchema>;
export type OtConsolidatedOrderItem = z.infer<
  typeof otConsolidatedOrderItemSchema
>;
export type OtSaveConsolidatedOrderRequest = z.infer<
  typeof otSaveConsolidatedOrderRequestSchema
>;
export type OtRule = z.infer<typeof otRuleSchema>;
export type IntegrationLogItem = z.infer<typeof integrationLogItemSchema>;
