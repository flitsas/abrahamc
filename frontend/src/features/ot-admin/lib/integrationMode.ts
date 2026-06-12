import type { OtIntegrationMode } from "../api/ot-admin.schemas.js";

export function isQuipuxMode(mode: string): boolean {
  return mode === "quipux" || mode === "qx";
}

export function normalizeIntegrationMode(mode: string): OtIntegrationMode {
  return isQuipuxMode(mode) ? "quipux" : "dashboard";
}

export function integrationModeLabel(mode: string): string {
  return isQuipuxMode(mode) ? "Quipux (simulación)" : "Dashboard FLIT";
}

export function toggleIntegrationMode(
  current: OtIntegrationMode,
): OtIntegrationMode {
  return current === "quipux" ? "dashboard" : "quipux";
}
