import type { ReactNode } from "react";

type AuthShellProps = {
  children: ReactNode;
  title: string;
  subtitle?: string;
};

export function AuthShell({ children, title, subtitle }: AuthShellProps) {
  return (
    <div className="flex min-h-screen font-flit bg-flit-bg text-flit-blueDark">
      <aside
        aria-hidden="true"
        className="relative hidden w-[42%] shrink-0 overflow-hidden bg-flit-sidebar lg:flex lg:flex-col lg:justify-between lg:rounded-r-flit-sidebar lg:p-12"
      >
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-white/80">
            FLIT
          </p>
          <h2 className="mt-4 max-w-sm text-3xl font-bold leading-tight text-white">
            Gestión de trámites vehiculares
          </h2>
          <p className="mt-4 max-w-md text-base leading-relaxed text-white/85">
            Plataforma administrativa para operadores y compañías de transporte.
          </p>
        </div>

        <ul className="space-y-4 text-sm text-white/90">
          <li className="flex items-center gap-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-white/15">
              <CheckIcon />
            </span>
            Trámites y traspasos en un solo panel
          </li>
          <li className="flex items-center gap-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-white/15">
              <CheckIcon />
            </span>
            Roles, permisos y multi-tenant
          </li>
          <li className="flex items-center gap-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-white/15">
              <CheckIcon />
            </span>
            Validación documental integrada
          </li>
        </ul>
      </aside>

      <main className="flex flex-1 items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-[440px] rounded-flit-card bg-flit-card p-8 shadow-flit-card sm:p-10">
          <p className="text-xs font-semibold uppercase tracking-wide text-flit-blueText">
            FLIT Trámites 2.0
          </p>
          <h1 className="mt-2 text-2xl font-bold text-flit-blueDark">
            {title}
          </h1>
          {subtitle && (
            <p className="mt-2 text-sm leading-relaxed text-flit-draft">
              {subtitle}
            </p>
          )}
          <div className="mt-8">{children}</div>
        </div>
      </main>
    </div>
  );
}

function CheckIcon() {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M20 6L9 17l-5-5" />
    </svg>
  );
}
