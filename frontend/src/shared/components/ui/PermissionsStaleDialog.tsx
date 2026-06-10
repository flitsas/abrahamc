import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { GradientButton } from "../flit/GradientButton.js";
import { subscribePermissionsStale } from "../../lib/permissionsStale.js";

export function PermissionsStaleDialog() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  useEffect(() => subscribePermissionsStale(() => setOpen(true)), []);

  if (!open) {
    return null;
  }

  function handleConfirm() {
    setOpen(false);
    navigate("/login?reason=permissions-stale", { replace: true });
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-flit-blueDark/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="permissions-stale-title"
      aria-describedby="permissions-stale-desc"
    >
      <div className="w-full max-w-md rounded-flit-card bg-flit-card p-6 shadow-flit-card">
        <h2
          id="permissions-stale-title"
          className="text-lg font-bold text-flit-blueText"
        >
          Permisos actualizados
        </h2>
        <p id="permissions-stale-desc" className="mt-3 text-sm text-flit-draft">
          Un administrador cambió sus roles o permisos. Por seguridad debe
          iniciar sesión nuevamente para continuar.
        </p>
        <div className="mt-6">
          <GradientButton
            type="button"
            className="!h-12 !text-sm"
            onClick={handleConfirm}
          >
            Entendido, ir a iniciar sesión
          </GradientButton>
        </div>
      </div>
    </div>
  );
}
