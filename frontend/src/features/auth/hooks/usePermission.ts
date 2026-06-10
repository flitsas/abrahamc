import { useAuthMe } from "../api/auth.api.js";
import { hasPermission } from "../lib/permissions.js";

export function usePermission(slug: string): boolean {
  const { data: session } = useAuthMe();
  if (!session) {
    return false;
  }
  return hasPermission(
    session.permissionSlugs,
    slug,
    session.user.isSuperAdmin,
  );
}
