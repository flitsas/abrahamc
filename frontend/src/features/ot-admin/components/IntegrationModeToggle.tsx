import type { OtIntegrationMode } from "../api/ot-admin.schemas.js";
import {
  integrationModeLabel,
  normalizeIntegrationMode,
} from "../lib/integrationMode.js";
import { usePatchOtIntegrationMode } from "../api/ot-admin.api.js";

type IntegrationModeToggleProps = {
  agencyId: string;
  currentMode: string;
  canAdminister: boolean;
};

export function IntegrationModeToggle({
  agencyId,
  currentMode,
  canAdminister,
}: IntegrationModeToggleProps) {
  const mutation = usePatchOtIntegrationMode(agencyId);
  const normalized = normalizeIntegrationMode(currentMode);

  if (!canAdminister) {
    return (
      <p className="text-sm text-flit-muted" role="status">
        Modo actual:{" "}
        <span className="font-semibold text-flit-blueText">
          {integrationModeLabel(normalized)}
        </span>
      </p>
    );
  }

  function handleToggle(target: OtIntegrationMode) {
    if (target === normalized || mutation.isPending) {
      return;
    }
    mutation.mutate(target);
  }

  return (
    <div
      className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4"
      role="group"
      aria-label="Modo de integración OT"
    >
      <span className="text-sm font-medium text-flit-blueText">
        Modo de integración
      </span>
      <div className="inline-flex rounded-flit-pill border border-flit-border bg-white p-1">
        <button
          type="button"
          aria-pressed={normalized === "dashboard"}
          disabled={mutation.isPending}
          onClick={() => handleToggle("dashboard")}
          className={`rounded-flit-pill px-4 py-2 text-sm font-semibold transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue ${
            normalized === "dashboard"
              ? "bg-flit-blue text-white"
              : "text-flit-blueText hover:bg-flit-bg"
          }`}
        >
          Dashboard FLIT
        </button>
        <button
          type="button"
          aria-pressed={normalized === "quipux"}
          disabled={mutation.isPending}
          onClick={() => handleToggle("quipux")}
          className={`rounded-flit-pill px-4 py-2 text-sm font-semibold transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue ${
            normalized === "quipux"
              ? "bg-flit-blue text-white"
              : "text-flit-blueText hover:bg-flit-bg"
          }`}
        >
          Quipux (simulación)
        </button>
      </div>
      {mutation.isError && (
        <p className="text-sm text-flit-danger" role="alert">
          No se pudo cambiar el modo. Intente nuevamente.
        </p>
      )}
    </div>
  );
}
