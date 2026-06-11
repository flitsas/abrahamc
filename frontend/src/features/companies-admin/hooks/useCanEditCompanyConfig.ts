import { useAuthMe } from "../../auth/api/auth.api.js";
import { canEditCompanyConfig } from "../../auth/lib/permissions.js";

export function useCanEditCompanyConfig(): boolean {
  const { data: session } = useAuthMe();
  if (!session) {
    return false;
  }
  return canEditCompanyConfig(
    session.permissionSlugs,
    session.user.isSuperAdmin,
  );
}
