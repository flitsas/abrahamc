import type { ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { useAuthMe, useLogout } from "../../../features/auth/api/auth.api.js";
import { HeaderNav } from "./HeaderNav.js";

type NavItem = {
  id: string;
  to: string;
  label: string;
  icon: ReactNode;
};

type AppShellProps = {
  navItems: NavItem[];
  children: ReactNode;
};

export function AppShell({ navItems, children }: AppShellProps) {
  const navigate = useNavigate();
  const { data: session } = useAuthMe();
  const logout = useLogout();

  async function handleLogout() {
    await logout.mutateAsync();
    navigate("/login", { replace: true });
  }

  const roleLabel = session?.user.isSuperAdmin ? "Superadmin" : "Operador";

  return (
    <div className="min-h-screen bg-flit-bg font-flit text-flit-blueDark">
      <HeaderNav
        navItems={navItems}
        userEmail={session?.user.email}
        roleLabel={roleLabel}
        tenantLabel={session?.user.tenantId}
        onLogout={() => void handleLogout()}
        logoutPending={logout.isPending}
      />

      <main className="flit-dashboard-canvas mx-auto max-w-[1440px] px-4 pb-8 pt-4 sm:px-8 sm:pb-10 sm:pt-6">
        {children}
      </main>
    </div>
  );
}
