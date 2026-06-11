import { z } from "zod";

export const companyRowSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  tenantName: z.string(),
  nit: z.string(),
  legalName: z.string(),
  commercialName: z.string().nullish(),
  status: z.string(),
  modulesEnabledJson: z.string(),
  createdAt: z.string(),
  updatedAt: z.string(),
  actions: z.object({
    canView: z.boolean(),
    canEdit: z.boolean(),
  }),
});

export const companiesPageSchema = z.object({
  data: z.array(companyRowSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export const moduleConfigSchema = z.object({
  id: z.string().uuid(),
  moduleKey: z.string(),
  configJson: z.string(),
  isActive: z.boolean(),
  version: z.number(),
  updatedAt: z.string(),
});

export const upsertModuleConfigResponseSchema = z.object({
  moduleKey: z.string(),
  configJson: z.string(),
  version: z.number(),
  updatedAt: z.string(),
  hotReloadApplied: z.boolean(),
});

export const vehicleExceptionSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  reason: z.string().nullish(),
  expiresAt: z.string().nullish(),
  createdAt: z.string(),
});

export const vehicleExceptionsSchema = z.array(vehicleExceptionSchema);

export const authorizedTrafficAgencySchema = z.object({
  id: z.string().uuid(),
  trafficAgencyId: z.string().uuid(),
  isEnabled: z.boolean(),
  updatedAt: z.string(),
});

export const authorizedTrafficAgenciesSchema = z.array(
  authorizedTrafficAgencySchema,
);

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

export const upsertModuleConfigRequestSchema = z.object({
  configJson: z.string(),
  isActive: z.boolean().default(true),
});

export const createCompanyRequestSchema = z.object({
  nit: z.string().min(1, "El NIT es obligatorio."),
  legalName: z.string().min(1, "La razón social es obligatoria."),
  commercialName: z.string().optional(),
  contactEmail: z.string().optional(),
  slug: z.string().optional(),
  modulesEnabledJson: z.string().optional(),
});

export const createCompanyResponseSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  nit: z.string(),
  legalName: z.string(),
  commercialName: z.string().nullish(),
  status: z.string(),
  modulesEnabledJson: z.string(),
  createdAt: z.string(),
});

export const updateCompanyRequestSchema = z.object({
  legalName: z.string().min(1, "La razón social es obligatoria."),
  commercialName: z.string().optional(),
  modulesEnabledJson: z.string().optional(),
});

export const updateCompanyResponseSchema = z.object({
  id: z.string().uuid(),
  updatedAt: z.string(),
});

export const createVehicleExceptionRequestSchema = z.object({
  userId: z.string().uuid(),
  reason: z.string().optional(),
  expiresAt: z.string().optional(),
});

export const upsertAuthorizedTrafficAgencyRequestSchema = z.object({
  trafficAgencyId: z.string().uuid(),
  isEnabled: z.boolean(),
});

export type CompanyRow = z.infer<typeof companyRowSchema>;
export type CompaniesPage = z.infer<typeof companiesPageSchema>;
export type ModuleConfig = z.infer<typeof moduleConfigSchema>;
export type VehicleException = z.infer<typeof vehicleExceptionSchema>;
export type AuthorizedTrafficAgency = z.infer<
  typeof authorizedTrafficAgencySchema
>;
export type OtAgency = z.infer<typeof otAgencySchema>;
export type UpsertModuleConfigRequest = z.infer<
  typeof upsertModuleConfigRequestSchema
>;
export type CreateCompanyRequest = z.infer<typeof createCompanyRequestSchema>;
export type CreateCompanyResponse = z.infer<typeof createCompanyResponseSchema>;
export type UpdateCompanyRequest = z.infer<typeof updateCompanyRequestSchema>;
export type UpdateCompanyResponse = z.infer<typeof updateCompanyResponseSchema>;
export type CreateVehicleExceptionRequest = z.infer<
  typeof createVehicleExceptionRequestSchema
>;

export const COMPANY_MODULE_KEYS = {
  registration: "registration",
  transfers: "transfers",
  company: "company",
  recaudo: "recaudo",
} as const;

export type CompanyModuleKey =
  (typeof COMPANY_MODULE_KEYS)[keyof typeof COMPANY_MODULE_KEYS];
