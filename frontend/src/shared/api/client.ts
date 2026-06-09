// shared/api/client.ts
import axios from "axios";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:3030/api/v1",
  headers: { "Content-Type": "application/json" },
  timeout: 10_000,
});

apiClient.interceptors.response.use(
  (res) => res,
  (error) => {
    const message =
      error.response?.data?.message ?? error.message ?? "Error de red";
    return Promise.reject(new Error(message));
  },
);
