import { PERMISSIONS } from "../../auth/lib/permissions.js";
import { usePermission } from "../../auth/hooks/usePermission.js";

export function useCanManageParametrizacion(): boolean {
  return usePermission(PERMISSIONS.manageParametrizacion);
}

export function useCanSimulateRules(): boolean {
  return usePermission(PERMISSIONS.simulateParametrizacionRules);
}
