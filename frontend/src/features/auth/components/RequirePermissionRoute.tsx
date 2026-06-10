import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";
import { useAuthMe } from "../api/auth.api.js";
import { hasPermission } from "../lib/permissions.js";
import { ForbiddenPage } from "../pages/ForbiddenPage.js";

type RequirePermissionRouteProps = {
  permission: string;
  children: ReactNode;
};

export function RequirePermissionRoute({
  permission,
  children,
}: RequirePermissionRouteProps) {
  const { data: session, isLoading, isError } = useAuthMe();

  if (isLoading) {
    return (
      <div className="p-6">
        <LoadingSkeleton rows={4} />
      </div>
    );
  }

  if (isError || !session) {
    return <Navigate to="/login" replace />;
  }

  const allowed = hasPermission(
    session.permissionSlugs,
    permission,
    session.user.isSuperAdmin,
  );

  if (!allowed) {
    return <ForbiddenPage />;
  }

  return <>{children}</>;
}
