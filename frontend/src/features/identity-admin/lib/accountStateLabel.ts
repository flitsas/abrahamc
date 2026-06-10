const labels: Record<string, string> = {
  active: "Activo",
  inactive: "Inactivo",
  pending: "Pendiente",
  temp_blocked: "Bloqueado temporal",
  permanent_blocked: "Bloqueado permanente",
};

export function accountStateLabel(state: string): string {
  return labels[state] ?? state;
}

export function accountStateVariant(
  state: string,
): "success" | "active" | "warning" | "danger" | "draft" {
  switch (state) {
    case "active":
      return "success";
    case "pending":
      return "warning";
    case "temp_blocked":
    case "permanent_blocked":
      return "danger";
    case "inactive":
      return "draft";
    default:
      return "active";
  }
}
