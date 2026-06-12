import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  integrationLogsPageSchema,
  otAgenciesPageSchema,
  otAgencySchema,
  otConsolidatedOrderSchema,
  otDashboardSchema,
  otIntegrationModeResponseSchema,
  otRulesListSchema,
  otSaveConsolidatedOrderRequestSchema,
  type OtIntegrationMode,
  type OtSaveConsolidatedOrderRequest,
} from "./ot-admin.schemas.js";

type AgencyListParams = {
  q?: string;
  page?: number;
  pageSize?: number;
};

type IntegrationLogsParams = {
  trafficAgencyId: string;
  page?: number;
  pageSize?: number;
};

export async function fetchOtAgencies(params: AgencyListParams = {}) {
  const { data } = await apiClient.get("/ot/agencies", {
    params: {
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
      q: params.q || undefined,
    },
  });
  return otAgenciesPageSchema.parse(data);
}

export async function fetchOtAgency(agencyId: string) {
  const { data } = await apiClient.get(`/ot/agencies/${agencyId}`);
  return otAgencySchema.parse(data);
}

export async function fetchOtDashboard(agencyId: string) {
  const { data } = await apiClient.get(`/ot/agencies/${agencyId}/dashboard`);
  return otDashboardSchema.parse(data);
}

export async function patchOtIntegrationMode(
  agencyId: string,
  mode: OtIntegrationMode,
) {
  const { data } = await apiClient.patch(
    `/ot/agencies/${agencyId}/integration-mode`,
    { mode },
  );
  return otIntegrationModeResponseSchema.parse(data);
}

export async function fetchOtConsolidatedOrder(agencyId: string) {
  const { data } = await apiClient.get(
    `/ot/agencies/${agencyId}/consolidated-order`,
  );
  return otConsolidatedOrderSchema.parse(data);
}

export async function saveOtConsolidatedOrder(
  agencyId: string,
  payload: OtSaveConsolidatedOrderRequest,
) {
  const body = otSaveConsolidatedOrderRequestSchema.parse(payload);
  const { data } = await apiClient.put(
    `/ot/agencies/${agencyId}/consolidated-order`,
    body,
  );
  return otConsolidatedOrderSchema.parse(data);
}

export async function fetchOtRules(agencyId: string) {
  const { data } = await apiClient.get(`/ot/agencies/${agencyId}/rules`);
  return otRulesListSchema.parse(data);
}

export async function fetchIntegrationLogs(params: IntegrationLogsParams) {
  const { data } = await apiClient.get("/integrations/logs", {
    params: {
      trafficAgencyId: params.trafficAgencyId,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    },
  });
  return integrationLogsPageSchema.parse(data);
}

export function useOtAgencies(params: AgencyListParams = {}, enabled = true) {
  return useQuery({
    queryKey: queryKeys.otAdmin.agencies(params),
    queryFn: () => fetchOtAgencies(params),
    enabled,
    staleTime: 30_000,
  });
}

export function useOtAgency(agencyId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: agencyId
      ? queryKeys.otAdmin.agency(agencyId)
      : ["ot-admin", "agency", "disabled"],
    queryFn: () => fetchOtAgency(agencyId!),
    enabled: enabled && Boolean(agencyId),
    staleTime: 30_000,
  });
}

export function useOtDashboard(agencyId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: agencyId
      ? queryKeys.otAdmin.dashboard(agencyId)
      : ["ot-admin", "dashboard", "disabled"],
    queryFn: () => fetchOtDashboard(agencyId!),
    enabled: enabled && Boolean(agencyId),
    staleTime: 15_000,
  });
}

export function useOtConsolidatedOrder(
  agencyId: string | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey: agencyId
      ? queryKeys.otAdmin.consolidatedOrder(agencyId)
      : ["ot-admin", "consolidated-order", "disabled"],
    queryFn: () => fetchOtConsolidatedOrder(agencyId!),
    enabled: enabled && Boolean(agencyId),
    staleTime: 15_000,
  });
}

export function useOtRules(agencyId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: agencyId
      ? queryKeys.otAdmin.rules(agencyId)
      : ["ot-admin", "rules", "disabled"],
    queryFn: () => fetchOtRules(agencyId!),
    enabled: enabled && Boolean(agencyId),
    staleTime: 30_000,
    retry: (failureCount, error) => {
      if (axios.isAxiosError(error) && error.response?.status === 403) {
        return false;
      }
      return failureCount < 2;
    },
  });
}

export function useIntegrationLogs(
  params: IntegrationLogsParams | null,
  enabled = true,
) {
  return useQuery({
    queryKey: params
      ? queryKeys.otAdmin.integrationLogs(params)
      : ["ot-admin", "integration-logs", "disabled"],
    queryFn: () => fetchIntegrationLogs(params!),
    enabled: enabled && params !== null,
    staleTime: 15_000,
    retry: (failureCount, error) => {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        return false;
      }
      return failureCount < 2;
    },
  });
}

export function usePatchOtIntegrationMode(agencyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (mode: OtIntegrationMode) =>
      patchOtIntegrationMode(agencyId, mode),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.otAdmin.dashboard(agencyId),
      });
      queryClient.invalidateQueries({
        queryKey: queryKeys.otAdmin.agency(agencyId),
      });
      queryClient.invalidateQueries({
        queryKey: ["ot-admin", "agencies"],
      });
    },
  });
}

export function useSaveOtConsolidatedOrder(agencyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: OtSaveConsolidatedOrderRequest) =>
      saveOtConsolidatedOrder(agencyId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.otAdmin.consolidatedOrder(agencyId),
      });
    },
  });
}
