import { z } from "zod";

export const procedureInstanceStateSchema = z.enum([
  "borrador",
  "asignado",
  "q_validacion",
  "pendiente",
  "aprobado",
  "enviado",
  "entregado",
  "rechazado",
  "anulado",
]);

export const procedureInstanceItemSchema = z.object({
  id: z.string().uuid(),
  compositeId: z.string(),
  referenceNumber: z.string(),
  state: z.string(),
  procedureTypeCode: z.string(),
  radicatedAt: z.string().nullish(),
});

export const procedureInstancesPageSchema = z.object({
  items: z.array(procedureInstanceItemSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export const listProcedureInstancesParamsSchema = z.object({
  tenantId: z.string().uuid(),
  page: z.number().int().min(1).default(1),
  pageSize: z.number().int().min(1).max(100).default(20),
  state: z.string().optional(),
  procedureTypeCode: z.string().optional(),
});

export const createProcedureInstanceRequestSchema = z.object({
  tenantId: z.string().uuid(),
  filedByUserId: z.string().uuid(),
  procedureTypeCode: z.string().min(1),
  trafficAgencyId: z.string().uuid().nullish(),
  edgeCode: z.string().min(1),
  omittedQueries: z.array(z.string()).optional(),
  fieldValues: z.record(z.string(), z.string().nullable()).optional(),
  documentTypeCode: z.string().optional(),
});

export const createProcedureInstanceResponseSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  procedureTypeId: z.string().uuid(),
  procedureTypeCode: z.string(),
  trafficAgencyId: z.string().uuid().nullish(),
  referenceNumber: z.string(),
  state: z.string(),
  configSchemaVersion: z.number(),
  createdAt: z.string(),
  createdBy: z.string().uuid(),
  primaryActorId: z.string().uuid().nullish(),
  vehicleId: z.string().uuid().nullish(),
  primaryActorEdgeRole: z.string().nullish(),
});

export type ProcedureInstanceItem = z.infer<typeof procedureInstanceItemSchema>;
export type ProcedureInstancesPage = z.infer<
  typeof procedureInstancesPageSchema
>;
export type ListProcedureInstancesParams = z.infer<
  typeof listProcedureInstancesParamsSchema
>;
export type CreateProcedureInstanceRequest = z.infer<
  typeof createProcedureInstanceRequestSchema
>;
export type CreateProcedureInstanceResponse = z.infer<
  typeof createProcedureInstanceResponseSchema
>;

export const procedureInstanceDetailSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  procedureTypeId: z.string().uuid(),
  trafficAgencyId: z.string().uuid().nullish(),
  referenceNumber: z.string(),
  state: z.string(),
  configSnapshot: z.string(),
  configSchemaVersion: z.number(),
  radicatedAt: z.string().nullish(),
  procedureTypeCode: z.string().optional(),
});

export type ProcedureInstanceDetail = z.infer<
  typeof procedureInstanceDetailSchema
>;
