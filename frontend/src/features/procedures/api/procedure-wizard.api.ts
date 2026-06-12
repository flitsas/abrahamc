import { useMutation, useQuery } from "@tanstack/react-query";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  ownershipValidateResponseSchema,
  procedureConfigurationSchema,
  queryResultsResponseSchema,
  runAsyncQueriesResponseSchema,
  type SaveOwnersRequest,
} from "./procedure-wizard.schemas.js";
import type { CreateProcedureInstanceRequest } from "./procedure-instances.schemas.js";
import { createProcedureInstanceResponseSchema } from "./procedure-instances.schemas.js";

export async function fetchProcedureConfiguration(
  typeCode: string,
  tenantId: string,
) {
  const { data } = await apiClient.get(
    `/procedures/types/${encodeURIComponent(typeCode)}/configuration`,
    { params: { tenantId } },
  );
  return procedureConfigurationSchema.parse(data);
}

export function useProcedureConfiguration(
  typeCode: string | undefined,
  tenantId: string | undefined,
) {
  return useQuery({
    queryKey:
      typeCode && tenantId
        ? queryKeys.procedures.configuration(typeCode, tenantId)
        : ["procedures", "configuration", "none"],
    queryFn: () => fetchProcedureConfiguration(typeCode!, tenantId!),
    enabled: Boolean(typeCode && tenantId),
    staleTime: 60_000,
  });
}

export async function runProcedureQueriesAsync(
  instanceId: string,
  body: {
    tenantId: string;
    executedByUserId: string;
    procedureTypeCode: string;
    edgeCode: string;
    documentTypeCode?: string;
    capturedFields?: Record<string, string | null>;
  },
) {
  const { data } = await apiClient.post(
    `/procedures/instances/${instanceId}/queries/run-async`,
    body,
  );
  return runAsyncQueriesResponseSchema.parse(data);
}

export async function fetchProcedureQueryResults(
  instanceId: string,
  tenantId: string,
) {
  const { data } = await apiClient.get(
    `/procedures/instances/${instanceId}/query-results`,
    { params: { tenantId } },
  );
  return queryResultsResponseSchema.parse(data);
}

export function useProcedureQueryResults(
  instanceId: string | undefined,
  tenantId: string | undefined,
  options?: { enabled?: boolean; refetchInterval?: number | false },
) {
  return useQuery({
    queryKey:
      instanceId && tenantId
        ? queryKeys.procedures.queryResults(instanceId, tenantId)
        : ["procedures", "query-results", "none"],
    queryFn: () => fetchProcedureQueryResults(instanceId!, tenantId!),
    enabled: Boolean(instanceId && tenantId) && (options?.enabled ?? true),
    refetchInterval: options?.refetchInterval ?? false,
    staleTime: 0,
  });
}

export async function saveProcedureOwners(
  instanceId: string,
  request: SaveOwnersRequest,
) {
  await apiClient.post(`/procedures/instances/${instanceId}/owners`, request);
}

export async function validateProcedureOwnership(
  instanceId: string,
  tenantId: string,
) {
  const { data } = await apiClient.get(
    `/procedures/instances/${instanceId}/ownership/validate`,
    { params: { tenantId } },
  );
  return ownershipValidateResponseSchema.parse(data);
}

export function useValidateOwnership(
  instanceId: string | undefined,
  tenantId: string | undefined,
  enabled: boolean,
) {
  return useQuery({
    queryKey:
      instanceId && tenantId
        ? queryKeys.procedures.ownershipValidate(instanceId, tenantId)
        : ["procedures", "ownership-validate", "none"],
    queryFn: () => validateProcedureOwnership(instanceId!, tenantId!),
    enabled: Boolean(instanceId && tenantId) && enabled,
    staleTime: 0,
  });
}

export function useCreateProcedureInstance() {
  return useMutation({
    mutationFn: async (request: CreateProcedureInstanceRequest) => {
      const { data } = await apiClient.post("/procedures/instances", request);
      return createProcedureInstanceResponseSchema.parse(data);
    },
  });
}

export function useSaveProcedureOwners() {
  return useMutation({
    mutationFn: ({
      instanceId,
      request,
    }: {
      instanceId: string;
      request: SaveOwnersRequest;
    }) => saveProcedureOwners(instanceId, request),
  });
}
