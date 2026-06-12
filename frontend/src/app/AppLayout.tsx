import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { RequirePermissionRoute } from "../features/auth/components/RequirePermissionRoute.js";
import { PERMISSIONS } from "../features/auth/lib/permissions.js";
import { usePermission } from "../features/auth/hooks/usePermission.js";
import { HomePage } from "../features/home/pages/HomePage.js";
import { UsersAdminPage } from "../features/identity-admin/pages/UsersAdminPage.js";
import { CompaniesAdminPage } from "../features/companies-admin/pages/CompaniesAdminPage.js";
import { ParametrizacionAdminPage } from "../features/parametrizador-admin/pages/ParametrizacionAdminPage.js";
import { OtTramitesConsolePage } from "../features/ot-admin/pages/OtTramitesConsolePage.js";
import { ProceduresPage } from "../features/procedures/pages/ProceduresPage.js";
import { ProcedureWizardPage } from "../features/procedures/pages/ProcedureWizardPage.js";
import { AppShell } from "../shared/components/flit/AppShell.js";

type NavItem = {
  id: string;
  to: string;
  label: string;
  icon: ReactNode;
};

const BASE_NAV_ITEMS: NavItem[] = [
  {
    id: "home",
    to: "/",
    label: "Inicio",
    icon: (
      <svg
        width="18"
        height="18"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M3 9.5L12 3l9 6.5V20a1 1 0 01-1 1h-5v-6H9v6H4a1 1 0 01-1-1V9.5z" />
      </svg>
    ),
  },
  {
    id: "procedures",
    to: "/tramites",
    label: "Trámites",
    icon: (
      <svg
        width="18"
        height="18"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
        <path d="M14 2v6h6M16 13H8M16 17H8M10 9H8" />
      </svg>
    ),
  },
];

const USERS_NAV_ITEM: NavItem = {
  id: "users",
  to: "/admin/usuarios",
  label: "Usuarios",
  icon: (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75" />
    </svg>
  ),
};

const PARAMETRIZACION_NAV_ITEM: NavItem = {
  id: "parametrizacion",
  to: "/admin/parametrizacion",
  label: "Parametrización",
  icon: (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M12 3l7 4v5c0 4-3 7-7 9-4-2-7-5-7-9V7l7-4z" />
      <path d="M9 12h6M12 9v6" />
    </svg>
  ),
};

const OT_TRAMITES_NAV_ITEM: NavItem = {
  id: "ot-tramites",
  to: "/admin/ot/tramites",
  label: "Consola OT",
  icon: (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M3 21h18M9 8h1M9 12h1M9 16h1M14 8h7M14 12h7M14 16h7" />
    </svg>
  ),
};

const COMPANIES_NAV_ITEM: NavItem = {
  id: "companies",
  to: "/admin/companies",
  label: "Compañías",
  icon: (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6" />
    </svg>
  ),
};

function useAppNavItems(): NavItem[] {
  const canViewTramites = usePermission(PERMISSIONS.viewTramites);
  const canManageUsers = usePermission(PERMISSIONS.manageUsers);
  const canViewCompanies = usePermission(PERMISSIONS.viewCompanies);
  const canViewParametrizacion = usePermission(PERMISSIONS.viewParametrizacion);
  const canOperateOt = usePermission(PERMISSIONS.otOperator);
  const items: NavItem[] = [BASE_NAV_ITEMS[0]];
  if (canViewTramites) {
    items.push(BASE_NAV_ITEMS[1]);
  }
  if (canManageUsers) {
    items.push(USERS_NAV_ITEM);
  }
  if (canViewCompanies) {
    items.push(COMPANIES_NAV_ITEM);
  }
  if (canViewParametrizacion) {
    items.push(PARAMETRIZACION_NAV_ITEM);
  }
  if (canOperateOt) {
    items.push(OT_TRAMITES_NAV_ITEM);
  }
  return items;
}

type AppLayoutProps = {
  previewPath?: string;
};

export function AppLayout({ previewPath }: AppLayoutProps) {
  const navItems = useAppNavItems();

  if (previewPath) {
    return (
      <AppShell navItems={navItems}>
        {previewPath === "/tramites" ? (
          <ProceduresPage />
        ) : previewPath === "/admin/usuarios" ? (
          <UsersAdminPage />
        ) : previewPath === "/admin/companies" ? (
          <CompaniesAdminPage />
        ) : previewPath === "/admin/parametrizacion" ? (
          <ParametrizacionAdminPage />
        ) : previewPath.startsWith("/admin/ot") ? (
          <OtTramitesConsolePage />
        ) : (
          <HomePage />
        )}
      </AppShell>
    );
  }

  return (
    <AppShell navItems={navItems}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route
          path="/tramites"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.viewTramites}>
              <ProceduresPage />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/tramites/nuevo/:typeCode"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.viewTramites}>
              <ProcedureWizardPage mode="new" />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/tramites/:instanceId"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.viewTramites}>
              <ProcedureWizardPage mode="continue" />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/admin/usuarios"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.manageUsers}>
              <UsersAdminPage />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/admin/companies"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.viewCompanies}>
              <CompaniesAdminPage />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/admin/parametrizacion"
          element={
            <RequirePermissionRoute
              permission={PERMISSIONS.viewParametrizacion}
            >
              <ParametrizacionAdminPage />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/admin/ot/tramites"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.otOperator}>
              <OtTramitesConsolePage />
            </RequirePermissionRoute>
          }
        />
        <Route
          path="/admin/ot/:agencyId/tramites"
          element={
            <RequirePermissionRoute permission={PERMISSIONS.otOperator}>
              <OtTramitesConsolePage />
            </RequirePermissionRoute>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}

export const APP_NAV_ITEMS = BASE_NAV_ITEMS;
