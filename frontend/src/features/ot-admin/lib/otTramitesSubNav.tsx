import type { ReactNode } from "react";

export type OtTramitesSubTabId =
  | "dashboard"
  | "orden-documentos"
  | "logs-integracion";

export type OtTramitesSubNavItem = {
  id: OtTramitesSubTabId;
  label: string;
  description: string;
  icon: ReactNode;
};

export function buildOtTramitesSubNavItems(): OtTramitesSubNavItem[] {
  return [
    {
      id: "dashboard",
      label: "Dashboard / QX",
      description:
        "Métricas FLIT, cola reciente y vista simulada Quipux según modo de integración.",
      icon: <DashboardIcon />,
    },
    {
      id: "orden-documentos",
      label: "Orden consolidado",
      description:
        "Reordene documentos del PDF consolidado con arrastrar y soltar.",
      icon: <OrderIcon />,
    },
    {
      id: "logs-integracion",
      label: "Logs integración",
      description:
        "Bitácora paginada de eventos webhook y llamadas externas del OT.",
      icon: <LogsIcon />,
    },
  ];
}

function DashboardIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <rect x="3" y="3" width="7" height="9" rx="1" />
      <rect x="14" y="3" width="7" height="5" rx="1" />
      <rect x="14" y="12" width="7" height="9" rx="1" />
      <rect x="3" y="16" width="7" height="5" rx="1" />
    </svg>
  );
}

function OrderIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />
    </svg>
  );
}

function LogsIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
      <path d="M14 2v6h6M8 13h8M8 17h5" />
    </svg>
  );
}
