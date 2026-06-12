import type { ReactNode } from "react";

export type ParametrizacionSubTabId =
  | "familias-tipos"
  | "pipeline"
  | "formularios"
  | "reglas";

export type ParametrizacionSubNavItem = {
  id: ParametrizacionSubTabId;
  label: string;
  description: string;
  icon: ReactNode;
};

export function buildParametrizacionSubNavItems(): ParametrizacionSubNavItem[] {
  return [
    {
      id: "familias-tipos",
      label: "Familias / Tipos",
      description:
        "Catálogo de familias de trámites y tipos configurados por tenant.",
      icon: <CatalogIcon />,
    },
    {
      id: "pipeline",
      label: "Pipeline",
      description:
        "Modelador visual del flujo: ordenar pasos y validar mínimo 4 activos antes de guardar.",
      icon: <PipelineIcon />,
    },
    {
      id: "formularios",
      label: "Formularios",
      description:
        "Editor dinámico de campos por paso con vista previa compatible con el backend.",
      icon: <FormIcon />,
    },
    {
      id: "reglas",
      label: "Reglas / Simulador",
      description:
        "Reglas del tipo seleccionado y simulador de evaluación con payload JSON.",
      icon: <RulesIcon />,
    },
  ];
}

function CatalogIcon() {
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
      <path d="M4 6h16M4 12h16M4 18h10" />
    </svg>
  );
}

function PipelineIcon() {
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
      <circle cx="6" cy="12" r="2" />
      <circle cx="12" cy="12" r="2" />
      <circle cx="18" cy="12" r="2" />
      <path d="M8 12h2M14 12h2" />
    </svg>
  );
}

function FormIcon() {
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

function RulesIcon() {
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
      <path d="M12 3l7 4v5c0 4-3 7-7 9-4-2-7-5-7-9V7l7-4z" />
      <path d="M9 12l2 2 4-4" />
    </svg>
  );
}
