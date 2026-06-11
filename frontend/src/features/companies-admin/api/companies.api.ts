import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient, ApiError } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  authorizedTrafficAgenciesSchema,
  companiesPageSchema,
  createVehicleExceptionRequestSchema,
  moduleConfigSchema,
  otAgenciesPageSchema,
  upsertModuleConfigRequestSchema,
  upsertModuleConfigResponseSchema,
  vehicleExceptionsSchema,
  type CompanyModuleKey,
  type CreateVehicleExceptionRequest,
  type UpsertModuleConfigRequest,
} from "./companies.schemas.js";

type ListCompaniesParams = {
  page: number;
  pageSize: number;
  search?: string;
};

export async function fetchCompanies(params: ListCompaniesParams) {
  const { data } = await apiClient.get("/companies", {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      search: params.search || undefined,
    },
  });
  return companiesPageSchema.parse(data);
}

export async function fetchCompanyModuleConfig(
  tenantId: string,
  moduleKey: CompanyModuleKey,
) {
  try {
    const { data } = await apiClient.get(
      `/companies/${tenantId}/module-configs/${moduleKey}`,
    );
    return moduleConfigSchema.parse(data);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }
    throw error;
  }
}

export async function upsertCompanyModuleConfig(
  tenantId: string,
  moduleKey: CompanyModuleKey,
  payload: UpsertModuleConfigRequest,
) {
  const body = upsertModuleConfigRequestSchema.parse(payload);
  const { data } = await apiClient.put(
    `/companies/${tenantId}/module-configs/${moduleKey}`,
    body,
  );
  return upsertModuleConfigResponseSchema.parse(data);
}

export async function fetchVehicleExceptions(tenantId: string) {
  const { data } = await apiClient.get(
    `/companies/${tenantId}/vehicle-exceptions`,
  );
  return vehicleExceptionsSchema.parse(data);
}

export async function createVehicleException(
  tenantId: string,
  payload: CreateVehicleExceptionRequest,
) {
  const body = createVehicleExceptionRequestSchema.parse(payload);
  const { data } = await apiClient.post(
    `/companies/${tenantId}/vehicle-exceptions`,
    body,
  );
  return data;
}

export async function deleteVehicleException(
  tenantId: string,
  exceptionId: string,
) {
  await apiClient.delete(
    `/companies/${tenantId}/vehicle-exceptions/${exceptionId}`,
  );
}

export async function fetchAuthorizedTrafficAgencies(tenantId: string) {
  const { data } = await apiClient.get(
    `/companies/${tenantId}/authorized-traffic-agencies`,
  );
  return authorizedTrafficAgenciesSchema.parse(data);
}

export async function upsertAuthorizedTrafficAgency(
  tenantId: string,
  trafficAgencyId: string,
  isEnabled: boolean,
) {
  const { data } = await apiClient.put(
    `/companies/${tenantId}/authorized-traffic-agencies`,
    { trafficAgencyId, isEnabled },
  );
  return data;
}

export async function fetchOtAgencies(pageSize = 100) {
  const { data } = await apiClient.get("/ot/agencies", {
    params: { page: 1, pageSize },
  });
  return otAgenciesPageSchema.parse(data);
}

export function useCompaniesList(
  params: ListCompaniesParams,
  enabled = true,
) {
  return useQuery({
    queryKey: queryKeys.companiesAdmin.list(params),
    queryFn: () => fetchCompanies(params),
    enabled,
    staleTime: 15_000,
  });
}

export function useCompanyModuleConfig(
  tenantId: string | undefined,
  moduleKey: CompanyModuleKey,
  enabled = true,
) {
  return useQuery({
    queryKey: tenantId
      ? queryKeys.companiesAdmin.moduleConfig(tenantId, moduleKey)
      : ["companies-admin", "module-config", "disabled"],
    queryFn: () => fetchCompanyModuleConfig(tenantId!, moduleKey),
    enabled: enabled && Boolean(tenantId),
    staleTime: 15_000,
  });
}

export function useUpsertModuleConfig(tenantId: string, moduleKey: CompanyModuleKey) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpsertModuleConfigRequest) =>
      upsertCompanyModuleConfig(tenantId, moduleKey, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.companiesAdmin.moduleConfig(tenantId, moduleKey),
      });
    },
  });
}

export function useVehicleExceptions(tenantId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: tenantId
      ? queryKeys.companiesAdmin.vehicleExceptions(tenantId)
      : ["companies-admin", "vehicle-exceptions", "disabled"],
    queryFn: () => fetchVehicleExceptions(tenantId!),
    enabled: enabled && Boolean(tenantId),
    staleTime: 15_000,
  });
}

export function useCreateVehicleException(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateVehicleExceptionRequest) =>
      createVehicleException(tenantId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.companiesAdmin.vehicleExceptions(tenantId),
      });
    },
  });
}

export function useDeleteVehicleException(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (exceptionId: string) =>
      deleteVehicleException(tenantId, exceptionId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.companiesAdmin.vehicleExceptions(tenantId),
      });
    },
  });
}

export function useAuthorizedTrafficAgencies(
  tenantId: string | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey: tenantId
      ? queryKeys.companiesAdmin.authorizedAgencies(tenantId)
      : ["companies-admin", "authorized-agencies", "disabled"],
    queryFn: () => fetchAuthorizedTrafficAgencies(tenantId!),
    enabled: enabled && Boolean(tenantId),
    staleTime: 15_000,
  });
}

export function useOtAgenciesCatalog(enabled = true) {
  return useQuery({
    queryKey: queryKeys.companiesAdmin.otCatalog,
    queryFn: () => fetchOtAgencies(),
    enabled,
    staleTime: 60_000,
  });
}

export function useUpsertAuthorizedTrafficAgency(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { trafficAgencyId: string; isEnabled: boolean }) =>
      upsertAuthorizedTrafficAgency(
        tenantId,
        input.trafficAgencyId,
        input.isEnabled,
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.companiesAdmin.authorizedAgencies(tenantId),
      });
    },
  });
}
