import type { ReactNode } from "react";
import { usePermission } from "../hooks/usePermission.js";

type PermissionGateProps = {
  permission: string;
  children: ReactNode;
};

export function PermissionGate({ permission, children }: PermissionGateProps) {
  const allowed = usePermission(permission);
  if (!allowed) {
    return null;
  }
  return <>{children}</>;
}
