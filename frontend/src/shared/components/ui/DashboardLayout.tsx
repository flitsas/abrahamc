import type { ReactNode } from "react";

type NavItem = {
  id: string;
  label: string;
  icon: ReactNode;
};

type DashboardLayoutProps = {
  navItems: NavItem[];
  activeSection: string;
  onNavigate: (section: string) => void;
  children: ReactNode;
};

export function DashboardLayout({
  navItems,
  activeSection,
  onNavigate,
  children,
}: DashboardLayoutProps) {
  return (
    <div className="flex min-h-screen bg-slate-50 text-slate-900">
      <aside className="w-64 border-r border-slate-200 bg-white">
        <div className="border-b border-slate-200 px-6 py-5">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
            FLIT
          </p>
          <h1 className="text-lg font-semibold">Panel administrativo</h1>
        </div>
        <nav aria-label="Secciones" className="p-3">
          <ul className="space-y-1">
            {navItems.map((item) => {
              const isActive = item.id === activeSection;
              return (
                <li key={item.id}>
                  <button
                    type="button"
                    onClick={() => onNavigate(item.id)}
                    aria-current={isActive ? "page" : undefined}
                    className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm font-medium transition ${
                      isActive
                        ? "bg-slate-900 text-white"
                        : "text-slate-700 hover:bg-slate-100"
                    }`}
                  >
                    <span className="shrink-0">{item.icon}</span>
                    <span>{item.label}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        </nav>
      </aside>
      <main className="flex-1 p-8">{children}</main>
    </div>
  );
}
