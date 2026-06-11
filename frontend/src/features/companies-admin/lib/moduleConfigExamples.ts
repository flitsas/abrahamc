import type { CompanyModuleKey } from "../api/companies.schemas.js";

export type ModuleConfigExample = {
  description: string;
  example: Record<string, unknown>;
};

export const MODULE_CONFIG_EXAMPLES: Record<
  CompanyModuleKey,
  ModuleConfigExample
> = {
  registration: {
    description:
      "Reglas del módulo de matrícula para este tenant (p. ej. organismo de tránsito por defecto).",
    example: {
      default_ot_code: "OT-MEDELLIN",
    },
  },
  transfers: {
    description:
      "Reglas del módulo de traspasos (p. ej. OT por defecto y validación SIMIT).",
    example: {
      default_ot_code: "OT-BOGOTA",
      require_simit: true,
    },
  },
  company: {
    description:
      "Comportamiento general de la compañía: flota propia, firmas y notificaciones.",
    example: {
      only_own_vehicles: true,
      seller_signature_mode: "digital_identity",
      buyer_signature_mode: "on_screen",
      signature_vault_enabled: true,
      notification_channel: "smtp",
      notification_target: "radicador",
    },
  },
  recaudo: {
    description: "Métodos de pago habilitados para operaciones de recaudo.",
    example: {
      payment_methods: ["flit_gateway", "ot", "other"],
    },
  },
};

export function formatModuleConfigExample(moduleKey: CompanyModuleKey): string {
  return JSON.stringify(MODULE_CONFIG_EXAMPLES[moduleKey].example, null, 2);
}
