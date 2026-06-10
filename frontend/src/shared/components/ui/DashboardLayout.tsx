import type { ReactNode } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuthMe, useLogout } from "../../../features/auth/api/auth.api.js";

type NavItem = {
  id: string;
  to: string;
  label: string;
  icon: ReactNode;
};

type DashboardLayoutProps = {
  navItems: NavItem[];
  children: ReactNode;
};

export function DashboardLayout({ navItems, children }: DashboardLayoutProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const { data: session } = useAuthMe();
  const logout = useLogout();

  async function handleLogout() {
    await logout.mutateAsync();
    navigate("/login", { replace: true });
  }

  return (
    <div className="flex min-h-screen bg-slate-50 text-slate-900">
      <aside className="w-64 border-r border-slate-200 bg-white">
        <div className="border-b border-slate-200 px-6 py-5">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
            FLIT
          </p>
          <h1 className="text-lg font-semibold">Panel administrativo</h1>
          {session && (
            <p className="mt-1 truncate text-xs text-slate-500">
              {session.user.email}
            </p>
          )}
        </div>
        <nav aria-label="Secciones" className="p-3">
          <ul className="space-y-1">
            {navItems.map((item) => {
              const isActive = location.pathname === item.to;
              return (
                <li key={item.id}>
                  <Link
                    to={item.to}
                    aria-current={isActive ? "page" : undefined}
                    className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm font-medium transition ${
                      isActive
                        ? "bg-slate-900 text-white"
                        : "text-slate-700 hover:bg-slate-100"
                    }`}
                  >
                    <span className="shrink-0">{item.icon}</span>
                    <span>{item.label}</span>
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
        <div className="border-t border-slate-200 p-3">
          <button
            type="button"
            onClick={() => void handleLogout()}
            disabled={logout.isPending}
            className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-slate-700 transition hover:bg-slate-100 disabled:opacity-60"
          >
            {logout.isPending ? "Cerrando sesión…" : "Cerrar sesión"}
          </button>
        </div>
      </aside>
      <main className="flex-1 p-8">{children}</main>
    </div>
  );
}
