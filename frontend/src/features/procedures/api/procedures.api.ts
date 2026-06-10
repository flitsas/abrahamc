import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import { procedureTypesResponseSchema } from "./procedures.schemas.js";

export async function fetchProcedureTypes(tenantId: string) {
  const { data } = await apiClient.get("/procedures/types", {
    params: { tenantId },
  });
  return procedureTypesResponseSchema.parse(data);
}

export function useProcedureTypes(tenantId: string | undefined) {
  return useQuery({
    queryKey: tenantId
      ? queryKeys.procedures.types(tenantId)
      : ["procedures", "types", "none"],
    queryFn: () => fetchProcedureTypes(tenantId!),
    enabled: Boolean(tenantId),
    staleTime: 30_000,
  });
}
