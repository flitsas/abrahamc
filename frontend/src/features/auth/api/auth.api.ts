import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  authMeResponseSchema,
  loginRequestSchema,
  loginResponseSchema,
  type AuthMeResponse,
  type LoginRequest,
} from "./auth.schemas.js";

export async function login(request: LoginRequest) {
  const payload = loginRequestSchema.parse(request);
  const { data } = await apiClient.post("/auth/login", payload);
  return loginResponseSchema.parse(data);
}

export async function fetchAuthMe(): Promise<AuthMeResponse | null> {
  try {
    const { data } = await apiClient.get("/auth/me");
    return authMeResponseSchema.parse(data);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }
    throw error;
  }
}

export async function logout() {
  await apiClient.post("/auth/logout", {});
}

export function useAuthMe(enabled = true) {
  return useQuery({
    queryKey: queryKeys.auth.me,
    queryFn: fetchAuthMe,
    enabled,
    retry: (failureCount, error) =>
      error instanceof ApiError &&
      error.status === undefined &&
      failureCount < 8,
    retryDelay: (attempt) => Math.min(1_000 * 2 ** attempt, 8_000),
    staleTime: 60_000,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: login,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.auth.me });
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: queryKeys.auth.me });
    },
  });
}
