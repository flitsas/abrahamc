import { PERMISSIONS } from "../../auth/lib/permissions.js";
import { usePermission } from "../../auth/hooks/usePermission.js";

export function useCanAdministerOt(): boolean {
  return usePermission(PERMISSIONS.otAdminister);
}

export function useCanReorderOtDocuments(): boolean {
  return usePermission(PERMISSIONS.otReorderDocuments);
}

export function useCanManageOtRules(): boolean {
  return usePermission(PERMISSIONS.otManageRules);
}
