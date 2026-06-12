import { useId, useState } from "react";
import { PermissionGate } from "../../auth/components/PermissionGate.js";
import { PERMISSIONS } from "../../auth/lib/permissions.js";
import { useAuthMe } from "../../auth/api/auth.api.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";
import { ProcedureInstancesPage } from "./ProcedureInstancesPage.js";
import { ProcedureTypesCatalog } from "../components/ProcedureTypesCatalog.js";

type ProceduresTab = "instances" | "types";

export function ProceduresPage() {
  const tabsId = useId();
  const { data: session } = useAuthMe();
  const tenantId = session?.user.tenantId;
  const [activeTab, setActiveTab] = useState<ProceduresTab>("instances");

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Trámites"
        subtitle="Gestione sus instancias radicadas y el catálogo de tipos disponibles."
        actions={
          <PermissionGate permission={PERMISSIONS.generateConsolidated}>
            <GradientButton
              type="button"
              className="!h-12 !w-auto !px-8 !text-sm"
              onClick={() => {
                // Placeholder: acción consolidado se implementará en feature OT
              }}
            >
              Generar consolidado
            </GradientButton>
          </PermissionGate>
        }
      />

      <div>
        <div
          role="tablist"
          aria-label="Secciones de trámites"
          className="flex flex-wrap gap-2 border-b border-flit-border pb-2"
        >
          <button
            type="button"
            role="tab"
            id={`${tabsId}-tab-instances`}
            aria-selected={activeTab === "instances"}
            aria-controls={`${tabsId}-panel-instances`}
            onClick={() => setActiveTab("instances")}
            className={`rounded-t-[10px] px-4 py-2 text-sm font-semibold focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue ${
              activeTab === "instances"
                ? "bg-flit-blue/10 text-flit-blueDark"
                : "text-flit-muted hover:text-flit-blueDark"
            }`}
          >
            Mis trámites
          </button>
          <PermissionGate permission={PERMISSIONS.viewTramites}>
            <button
              type="button"
              role="tab"
              id={`${tabsId}-tab-types`}
              aria-selected={activeTab === "types"}
              aria-controls={`${tabsId}-panel-types`}
              onClick={() => setActiveTab("types")}
              className={`rounded-t-[10px] px-4 py-2 text-sm font-semibold focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue ${
                activeTab === "types"
                  ? "bg-flit-blue/10 text-flit-blueDark"
                  : "text-flit-muted hover:text-flit-blueDark"
              }`}
            >
              Catálogo de tipos
            </button>
          </PermissionGate>
          <PermissionGate permission={PERMISSIONS.tramitesMaestro}>
            <a
              href="/admin/parametrizacion"
              className="ml-auto self-center text-xs font-semibold text-flit-blue underline"
            >
              Administrar maestro
            </a>
          </PermissionGate>
        </div>

        <div className="pt-6">
          {activeTab === "instances" && (
            <div
              role="tabpanel"
              id={`${tabsId}-panel-instances`}
              aria-labelledby={`${tabsId}-tab-instances`}
            >
              <ProcedureInstancesPage />
            </div>
          )}
          {activeTab === "types" && (
            <div
              role="tabpanel"
              id={`${tabsId}-panel-types`}
              aria-labelledby={`${tabsId}-tab-types`}
            >
              <ProcedureTypesCatalog tenantId={tenantId} />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
