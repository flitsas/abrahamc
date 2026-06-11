export const PERMISSIONS = {
  manageUsers: "modulo.identidad.gestionar-usuarios",
  generateConsolidated: "ui.tramites.generar-consolidado",
  viewCompanies: "modulo.companias.ver",
  manageCompanies: "modulo.companias.gestionar",
  editCompanyConfig: "ui.companias.editar-config",
  crudTotalCompanies: "modulo.companias.crud-total",
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

export function canEditCompanyConfig(
  permissionSlugs: readonly string[],
  isSuperAdmin = false,
): boolean {
  if (isSuperAdmin) {
    return true;
  }
  return (
    permissionSlugs.includes(PERMISSIONS.editCompanyConfig) ||
    permissionSlugs.includes(PERMISSIONS.crudTotalCompanies)
  );
}
