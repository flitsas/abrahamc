import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  createProcedureInstanceResponseSchema,
  procedureInstanceDetailSchema,
  procedureInstancesPageSchema,
  type CreateProcedureInstanceRequest,
  type ListProcedureInstancesParams,
} from "./procedure-instances.schemas.js";

export async function fetchProcedureInstances(
  params: ListProcedureInstancesParams,
) {
  const { data } = await apiClient.get("/procedures/instances", {
    params: {
      tenantId: params.tenantId,
      page: params.page,
      pageSize: params.pageSize,
      ...(params.state ? { state: params.state } : {}),
      ...(params.procedureTypeCode
        ? { procedureTypeCode: params.procedureTypeCode }
        : {}),
    },
  });
  return procedureInstancesPageSchema.parse(data);
}

export function useProcedureInstances(
  params: ListProcedureInstancesParams | undefined,
) {
  return useQuery({
    queryKey: params
      ? queryKeys.procedures.instances(params)
      : ["procedures", "instances", "none"],
    queryFn: () => fetchProcedureInstances(params!),
    enabled: Boolean(params?.tenantId),
    staleTime: 15_000,
  });
}

export async function createProcedureInstance(
  request: CreateProcedureInstanceRequest,
) {
  const { data } = await apiClient.post("/procedures/instances", request);
  return createProcedureInstanceResponseSchema.parse(data);
}

export async function fetchProcedureInstance(
  instanceId: string,
  tenantId: string,
) {
  const { data } = await apiClient.get(`/procedures/instances/${instanceId}`, {
    params: { tenantId },
  });
  return procedureInstanceDetailSchema.parse(data);
}

export function useProcedureInstance(
  instanceId: string | undefined,
  tenantId: string | undefined,
) {
  return useQuery({
    queryKey:
      instanceId && tenantId
        ? queryKeys.procedures.instanceDetail(instanceId, tenantId)
        : ["procedures", "instance-detail", "none"],
    queryFn: () => fetchProcedureInstance(instanceId!, tenantId!),
    enabled: Boolean(instanceId && tenantId),
    staleTime: 15_000,
  });
}
