type StateChipVariant = "success" | "active" | "warning" | "danger" | "draft";

const STATE_VARIANTS: Record<string, StateChipVariant> = {
  borrador: "draft",
  asignado: "active",
  q_validacion: "warning",
  pendiente: "warning",
  aprobado: "success",
  enviado: "active",
  entregado: "success",
  rechazado: "danger",
  anulado: "danger",
};

export function formatProcedureState(state: string): string {
  return state.replace(/_/g, " ");
}

export function procedureStateVariant(state: string): StateChipVariant {
  return STATE_VARIANTS[state.toLowerCase()] ?? "active";
}
