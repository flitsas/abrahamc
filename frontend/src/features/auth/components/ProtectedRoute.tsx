import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";
import { useAuthMe } from "../api/auth.api.js";

type ProtectedRouteProps = {
  children: ReactNode;
};

export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const location = useLocation();
  const { data, isLoading, isError } = useAuthMe();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-flit-bg p-8">
        <div className="w-full max-w-md">
          <LoadingSkeleton rows={3} />
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return <>{children}</>;
}
