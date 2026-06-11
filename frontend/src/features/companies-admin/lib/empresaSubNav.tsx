import type { CompanyConfigSubNavItem } from "../components/CompanyConfigSubNav.js";

export type EmpresaSubTabId = "general" | "matriz-ot" | "excepciones";

export function buildEmpresaSubNavItems(
  dirty: Partial<Record<EmpresaSubTabId, boolean>>,
): CompanyConfigSubNavItem[] {
  return [
    {
      id: "general",
      label: "General",
      description:
        "Comportamiento general de la compañía: flota propia, firmas, notificaciones y reglas JSON.",
      icon: <GeneralIcon />,
      dirty: dirty.general,
    },
    {
      id: "matriz-ot",
      label: "Matriz OT",
      description:
        "Organismos de tránsito habilitados para operar trámites en este tenant.",
      icon: <OtMatrixIcon />,
      dirty: dirty["matriz-ot"],
    },
    {
      id: "excepciones",
      label: "Excepciones",
      description:
        "Lista blanca de usuarios autorizados para operar vehículos de terceros.",
      icon: <ExceptionsIcon />,
      dirty: dirty.excepciones,
    },
  ];
}

function GeneralIcon() {
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
      <path d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6" />
    </svg>
  );
}

function OtMatrixIcon() {
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
      <path d="M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z" />
    </svg>
  );
}

function ExceptionsIcon() {
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
      <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 11l-2 2-1.5-1.5" />
    </svg>
  );
}
