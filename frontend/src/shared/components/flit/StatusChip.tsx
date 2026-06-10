type StatusChipVariant = "success" | "active" | "warning" | "danger" | "draft";

type StatusChipProps = {
  label: string;
  variant?: StatusChipVariant;
};

const variantClasses: Record<StatusChipVariant, string> = {
  success: "bg-flit-green/15 text-flit-green",
  active: "bg-flit-blue/15 text-flit-blue",
  warning: "bg-flit-warning/15 text-flit-warning",
  danger: "bg-flit-danger/15 text-flit-danger",
  draft: "bg-flit-draft/10 text-flit-draft",
};

export function StatusChip({ label, variant = "active" }: StatusChipProps) {
  return (
    <span
      className={`inline-flex items-center rounded-flit-pill px-3 py-1 text-xs font-semibold uppercase tracking-wide ${variantClasses[variant]}`}
    >
      {label}
    </span>
  );
}
