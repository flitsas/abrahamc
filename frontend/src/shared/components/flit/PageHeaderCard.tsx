import type { ReactNode } from "react";

type PageHeaderCardProps = {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
};

export function PageHeaderCard({
  title,
  subtitle,
  actions,
}: PageHeaderCardProps) {
  return (
    <header className="rounded-flit-card bg-flit-card px-6 py-5 shadow-flit-card sm:px-8 sm:py-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-flit-blueText">
            FLIT Trámites 2.0
          </p>
          <h1 className="mt-1 text-2xl font-bold text-flit-blueText sm:text-3xl">
            {title}
          </h1>
          {subtitle && (
            <p className="mt-2 max-w-2xl text-sm leading-relaxed text-flit-draft">
              {subtitle}
            </p>
          )}
        </div>
        {actions && <div className="shrink-0">{actions}</div>}
      </div>
    </header>
  );
}
