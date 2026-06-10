import type { ReactNode } from "react";
import { StatusChip } from "./StatusChip.js";

type KpiCardProps = {
  label: string;
  value: ReactNode;
  hint?: string;
  icon?: ReactNode;
  status?: {
    label: string;
    variant: "success" | "active" | "warning" | "danger" | "draft";
  };
  futuristic?: boolean;
};

export function KpiCard({
  label,
  value,
  hint,
  icon,
  status,
  futuristic = false,
}: KpiCardProps) {
  return (
    <article
      className={`relative overflow-hidden rounded-flit-card bg-flit-card p-6 shadow-flit-card transition duration-300 hover:-translate-y-0.5 hover:shadow-[0_12px_32px_rgba(22,39,68,0.12)] ${
        futuristic ? "flit-kpi-futuristic" : ""
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-flit-muted">
          {label}
        </p>
        {icon && (
          <span
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-flit-bg text-flit-blue"
            aria-hidden="true"
          >
            {icon}
          </span>
        )}
      </div>

      <div
        className={`mt-4 text-3xl font-bold leading-none ${
          futuristic ? "flit-kpi-value" : "text-flit-blueDark"
        }`}
      >
        {value}
      </div>

      {(hint || status) && (
        <div className="mt-4 flex flex-wrap items-center gap-2">
          {status && (
            <StatusChip label={status.label} variant={status.variant} />
          )}
          {hint && <p className="text-sm text-flit-draft">{hint}</p>}
        </div>
      )}
    </article>
  );
}
