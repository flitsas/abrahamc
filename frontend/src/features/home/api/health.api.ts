import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import { healthResponseSchema } from "./health.schemas.js";

const healthBaseUrl =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/api\/v1\/?$/, "") ??
  "http://localhost:3030";

export async function fetchHealth() {
  const { data } = await axios.get(`${healthBaseUrl}/api/v1/health`, {
    timeout: 5_000,
  });
  return healthResponseSchema.parse(data);
}

export function useHealth() {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: fetchHealth,
    staleTime: 15_000,
    retry: 1,
  });
}
