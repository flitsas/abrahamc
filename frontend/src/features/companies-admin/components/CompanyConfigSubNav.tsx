import type { ReactNode } from "react";

export type CompanyConfigSubNavItem = {
  id: string;
  label: string;
  description: string;
  icon: ReactNode;
  dirty?: boolean;
};

type CompanyConfigSubNavProps = {
  items: CompanyConfigSubNavItem[];
  activeId: string;
  onChange: (id: string) => void;
  ariaLabel: string;
  idPrefix: string;
};

export function CompanyConfigSubNav({
  items,
  activeId,
  onChange,
  ariaLabel,
  idPrefix,
}: CompanyConfigSubNavProps) {
  const activeItem = items.find((item) => item.id === activeId);

  return (
    <div className="space-y-4">
      <nav
        aria-label={ariaLabel}
        className="flit-nav-dock flex flex-wrap items-center justify-start gap-2 overflow-x-auto p-2"
      >
        {items.map((item) => {
          const selected = activeId === item.id;
          return (
            <button
              key={item.id}
              type="button"
              id={`${idPrefix}-sub-${item.id}`}
              aria-current={selected ? "page" : undefined}
              onClick={() => onChange(item.id)}
              className={`flit-nav-pill inline-flex shrink-0 items-center gap-2 rounded-flit-pill px-3 py-2 text-sm font-semibold transition sm:px-4 sm:py-2.5 ${
                selected ? "flit-nav-pill-active" : "flit-nav-pill-idle"
              }`}
            >
              <span
                className={`flex h-7 w-7 items-center justify-center rounded-full ${
                  selected
                    ? "bg-white/20 text-white"
                    : "bg-flit-bg text-flit-blue"
                }`}
              >
                {item.icon}
              </span>
              <span>
                {item.label}
                {item.dirty ? " *" : ""}
              </span>
            </button>
          );
        })}
      </nav>

      {activeItem && (
        <p className="text-sm text-flit-muted">{activeItem.description}</p>
      )}
    </div>
  );
}
