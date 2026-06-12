import axios from "axios";

export type FieldErrors = Partial<Record<string, string>>;

const FIELD_HINTS: ReadonlyArray<{ pattern: RegExp; field: string }> = [
  { pattern: /\bcode\b/i, field: "code" },
  { pattern: /\bname\b/i, field: "name" },
  { pattern: /\bfamily/i, field: "familyCode" },
  { pattern: /\bslug\b/i, field: "slug" },
  { pattern: /\bfieldKey\b/i, field: "fieldKey" },
  { pattern: /\blabel\b/i, field: "label" },
  { pattern: /\bsectionKey\b/i, field: "sectionKey" },
  { pattern: /\btitle\b/i, field: "title" },
  { pattern: /\btenantId\b/i, field: "tenantId" },
  { pattern: /pipeline|pasos activos/i, field: "edges" },
];

export function parseApiFieldErrors(error: unknown): {
  general: string | null;
  fields: FieldErrors;
} {
  if (!axios.isAxiosError(error)) {
    return {
      general: error instanceof Error ? error.message : "Error inesperado.",
      fields: {},
    };
  }

  const data = error.response?.data as
    | {
        error?: string;
        message?: string;
        field?: string;
        fields?: Record<string, string>;
      }
    | undefined;

  const message = data?.message ?? data?.error ?? error.message;
  const fields: FieldErrors = { ...(data?.fields ?? {}) };

  if (data?.field && message) {
    fields[data.field] = message;
  }

  if (message && Object.keys(fields).length === 0) {
    for (const hint of FIELD_HINTS) {
      if (hint.pattern.test(message)) {
        fields[hint.field] = message;
        break;
      }
    }
  }

  const hasFieldErrors = Object.keys(fields).length > 0;
  return {
    general: hasFieldErrors ? null : (message ?? "Error de la API."),
    fields,
  };
}
