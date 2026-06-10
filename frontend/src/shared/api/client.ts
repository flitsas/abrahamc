import axios from "axios";
import { notifyPermissionsStale } from "../lib/permissionsStale.js";

/**
 * En desarrollo usamos ruta relativa para que el proxy de Vite (`/api` → :3030)
 * evite CORS y "Network Error" cuando solo corre `pnpm dev:frontend`.
 */
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "/api/v1",
  headers: { "Content-Type": "application/json" },
  timeout: 10_000,
  withCredentials: true,
});

export class ApiError extends Error {
  readonly status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

function resolveApiErrorMessage(error: unknown): string {
  if (!axios.isAxiosError(error)) {
    return "Error inesperado al conectar con la API.";
  }

  if (error.code === "ECONNABORTED") {
    return "La API tardó demasiado en responder. Verifica que el backend esté en marcha.";
  }

  if (!error.response) {
    return "No se pudo conectar con la API. Ejecuta «pnpm run dev:api» o «pnpm run dev» y confirma que Postgres esté activo.";
  }

  const data = error.response.data as
    | { message?: string; error?: string }
    | undefined;
  return data?.message ?? data?.error ?? error.message ?? "Error de la API.";
}

apiClient.interceptors.response.use(
  (res) => res,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 403) {
      const data = error.response.data as { error?: string } | undefined;
      if (data?.error === "PERMISSIONS_STALE") {
        if (!window.location.pathname.startsWith("/login")) {
          notifyPermissionsStale();
        }
      }
    }

    return Promise.reject(
      new ApiError(
        resolveApiErrorMessage(error),
        axios.isAxiosError(error) ? error.response?.status : undefined,
      ),
    );
  },
);
