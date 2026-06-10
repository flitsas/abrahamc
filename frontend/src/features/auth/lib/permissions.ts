export const PERMISSIONS = {
  manageUsers: "modulo.identidad.gestionar-usuarios",
  generateConsolidated: "ui.tramites.generar-consolidado",
} as const;

export type PermissionSlug =
  | (typeof PERMISSIONS)[keyof typeof PERMISSIONS]
  | string;

export function hasPermission(
  permissionSlugs: readonly string[],
  slug: string,
  isSuperAdmin = false,
): boolean {
  if (isSuperAdmin) {
    return true;
  }
  return permissionSlugs.includes(slug);
}
