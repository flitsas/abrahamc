import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";

type NavItem = {
  id: string;
  to: string;
  label: string;
  icon: ReactNode;
};

type HeaderNavProps = {
  navItems: NavItem[];
  userEmail?: string;
  roleLabel?: string;
  tenantLabel?: string;
  onLogout?: () => void;
  logoutPending?: boolean;
};

export function HeaderNav({
  navItems,
  userEmail,
  roleLabel = "Operador",
  tenantLabel,
  onLogout,
  logoutPending,
}: HeaderNavProps) {
  const location = useLocation();

  return (
    <header className="sticky top-0 z-40">
      <div className="flit-header-band relative overflow-hidden px-4 pb-14 pt-4 sm:px-8 sm:pb-16 sm:pt-5">
        <div
          className="pointer-events-none absolute -right-16 -top-10 h-48 w-48 rounded-full bg-white/10 blur-2xl"
          aria-hidden="true"
        />
        <div
          className="pointer-events-none absolute -bottom-8 left-1/4 h-32 w-32 rounded-full bg-white/8 blur-xl"
          aria-hidden="true"
        />

        <div className="relative mx-auto flex max-w-[1440px] items-center justify-between gap-4">
          <Link
            to="/"
            className="group flex items-center gap-3 rounded-2xl px-1 py-1 transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-white"
          >
            <span className="flit-brand-medallion flex h-11 w-11 items-center justify-center sm:h-12 sm:w-12">
              <img
                src="/flitsas_logo.jpg"
                alt=""
                className="h-8 w-8 rounded-full object-cover sm:h-9 sm:w-9"
                draggable={false}
              />
            </span>
            <span>
              <p className="text-[10px] font-semibold uppercase tracking-[0.32em] text-white/75">
                FLIT
              </p>
              <p className="text-sm font-bold text-white sm:text-base">
                Trámites 2.0
              </p>
            </span>
          </Link>

          <div className="flex items-center gap-2 sm:gap-3">
            <button
              type="button"
              aria-label="Notificaciones"
              className="flit-header-action flex h-10 w-10 items-center justify-center rounded-full text-white transition hover:bg-white/20 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
            >
              <BellIcon />
            </button>

            <div className="flit-header-action hidden items-center gap-2 rounded-flit-pill px-3 py-2 lg:flex">
              <span className="text-xs font-semibold uppercase tracking-wide text-white">
                {roleLabel}
              </span>
              {tenantLabel && (
                <>
                  <span className="h-3 w-px bg-white/25" aria-hidden="true" />
                  <span className="max-w-[120px] truncate text-xs text-white/80">
                    {tenantLabel.slice(0, 8)}…
                  </span>
                </>
              )}
            </div>

            <div className="flit-header-action flex items-center gap-2 rounded-flit-pill px-2 py-1.5 sm:px-3">
              <span
                className="flex h-8 w-8 items-center justify-center rounded-full bg-white text-xs font-bold text-flit-blue"
                aria-hidden="true"
              >
                {userEmail?.charAt(0).toUpperCase() ?? "U"}
              </span>
              <span className="hidden max-w-[150px] truncate text-sm font-medium text-white md:inline">
                {userEmail ?? "Usuario"}
              </span>
            </div>

            <button
              type="button"
              aria-label="Cerrar sesión"
              onClick={onLogout}
              disabled={logoutPending}
              className="flit-header-action flex h-10 w-10 items-center justify-center rounded-full text-white/90 transition hover:bg-white/20 hover:text-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white disabled:opacity-60"
            >
              <LogoutIcon />
            </button>
          </div>
        </div>
      </div>

      <div className="relative -mt-10 px-4 sm:-mt-11 sm:px-8">
        <nav
          aria-label="Módulos principales"
          className="flit-nav-dock mx-auto flex max-w-[1440px] items-center justify-center gap-2 overflow-x-auto p-2 sm:gap-3 sm:p-2.5"
        >
          {navItems.map((item) => {
            const isActive = location.pathname === item.to;
            return (
              <Link
                key={item.id}
                to={item.to}
                aria-current={isActive ? "page" : undefined}
                className={`flit-nav-pill inline-flex shrink-0 items-center gap-2.5 rounded-flit-pill px-4 py-2.5 text-sm font-semibold transition sm:px-5 sm:py-3 ${
                  isActive ? "flit-nav-pill-active" : "flit-nav-pill-idle"
                }`}
              >
                <span
                  className={`flex h-8 w-8 items-center justify-center rounded-full ${
                    isActive
                      ? "bg-white/20 text-white"
                      : "bg-flit-bg text-flit-blue"
                  }`}
                >
                  {item.icon}
                </span>
                <span>{item.label}</span>
              </Link>
            );
          })}
        </nav>
      </div>
    </header>
  );
}

function BellIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M18 8a6 6 0 10-12 0c0 7-3 7-3 7h18s-3 0-3-7" />
      <path d="M13.7 21a2 2 0 01-3.4 0" />
    </svg>
  );
}

function LogoutIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4" />
      <path d="M16 17l5-5-5-5M21 12H9" />
    </svg>
  );
}
