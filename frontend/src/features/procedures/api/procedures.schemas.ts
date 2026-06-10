import { z } from "zod";

export const procedureTypeSchema = z.object({
  id: z.string().uuid(),
  code: z.string(),
  slug: z.string(),
  name: z.string(),
  familyCode: z.string(),
  familyName: z.string(),
  maxSteps: z.number(),
});

export const procedureTypesResponseSchema = z.object({
  items: z.array(procedureTypeSchema),
});

export type ProcedureType = z.infer<typeof procedureTypeSchema>;
