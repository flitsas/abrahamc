import { Navigate, Route, Routes } from "react-router-dom";
import { HomePage } from "../features/home/pages/HomePage.js";
import { ProceduresPage } from "../features/procedures/pages/ProceduresPage.js";
import { AppShell } from "../shared/components/flit/AppShell.js";

export const APP_NAV_ITEMS = [
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

type AppLayoutProps = {
  previewPath?: string;
};

export function AppLayout({ previewPath }: AppLayoutProps) {
  if (previewPath) {
    return (
      <AppShell navItems={APP_NAV_ITEMS}>
        {previewPath === "/tramites" ? <ProceduresPage /> : <HomePage />}
      </AppShell>
    );
  }

  return (
    <AppShell navItems={APP_NAV_ITEMS}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/tramites" element={<ProceduresPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}
