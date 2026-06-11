import { useCallback, useEffect, useId, useState } from "react";
import type { CompanyRow } from "../api/companies.schemas.js";
import { COMPANY_MODULE_KEYS } from "../api/companies.schemas.js";
import { ModuleConfigForm } from "./ModuleConfigForm.js";
import { OtMatrixSection } from "./OtMatrixSection.js";
import { VehicleExceptionsSection } from "./VehicleExceptionsSection.js";

type ConfigTabId = "matricula" | "traspasos" | "empresa" | "recaudo";

const TABS: { id: ConfigTabId; label: string }[] = [
  { id: "matricula", label: "Matrícula" },
  { id: "traspasos", label: "Traspasos" },
  { id: "empresa", label: "Empresa" },
  { id: "recaudo", label: "Recaudo" },
];

type CompanyConfigTabsProps = {
  company: CompanyRow;
};

export function CompanyConfigTabs({ company }: CompanyConfigTabsProps) {
  const tabsId = useId();
  const [activeTab, setActiveTab] = useState<ConfigTabId>("matricula");
  const [dirtyTabs, setDirtyTabs] = useState<Partial<Record<ConfigTabId, boolean>>>(
    {},
  );
  const [empresaModuleDirty, setEmpresaModuleDirty] = useState(false);
  const [empresaOtDirty, setEmpresaOtDirty] = useState(false);

  const setTabDirty = useCallback((tabId: ConfigTabId, dirty: boolean) => {
    setDirtyTabs((current) => {
      if (current[tabId] === dirty) {
        return current;
      }
      return { ...current, [tabId]: dirty };
    });
  }, []);

  useEffect(() => {
    setDirtyTabs((current) => {
      const combined = empresaModuleDirty || empresaOtDirty;
      if (current.empresa === combined) {
        return current;
      }
      return { ...current, empresa: combined };
    });
  }, [empresaModuleDirty, empresaOtDirty]);

  function requestTabChange(nextTab: ConfigTabId) {
    if (nextTab === activeTab) {
      return;
    }

    if (dirtyTabs[activeTab]) {
      const confirmed = window.confirm(
        "Hay cambios sin guardar en esta pestaña. ¿Desea cambiar sin guardar?",
      );
      if (!confirmed) {
        return;
      }
      setDirtyTabs((current) => ({ ...current, [activeTab]: false }));
      if (activeTab === "empresa") {
        setEmpresaModuleDirty(false);
        setEmpresaOtDirty(false);
      }
    }

    setActiveTab(nextTab);
  }

  return (
    <section
      className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-6 shadow-flit-card"
      aria-labelledby={`${tabsId}-heading`}
    >
      <header className="mb-6">
        <h2
          id={`${tabsId}-heading`}
          className="text-lg font-bold text-flit-blueDark"
        >
          Configuración — {company.legalName}
        </h2>
        <p className="mt-1 text-sm text-flit-muted">
          NIT {company.nit} · Tenant {company.tenantName}
        </p>
      </header>

      <div
        role="tablist"
        aria-label="Configuración por módulo"
        className="flex flex-wrap gap-2 border-b border-flit-draft/15 pb-3"
      >
        {TABS.map((tab) => {
          const selected = activeTab === tab.id;
          const dirty = Boolean(dirtyTabs[tab.id]);
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              id={`${tabsId}-tab-${tab.id}`}
              aria-selected={selected}
              aria-controls={`${tabsId}-panel-${tab.id}`}
              tabIndex={selected ? 0 : -1}
              onClick={() => requestTabChange(tab.id)}
              className={`rounded-flit-pill px-4 py-2 text-sm font-semibold focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue ${
                selected
                  ? "bg-flit-blue text-white"
                  : "border border-flit-draft/30 text-flit-blueDark hover:bg-flit-bg"
              }`}
            >
              {tab.label}
              {dirty ? " *" : ""}
            </button>
          );
        })}
      </div>

      <div className="mt-6">
        {activeTab === "matricula" && (
          <div
            role="tabpanel"
            id={`${tabsId}-panel-matricula`}
            aria-labelledby={`${tabsId}-tab-matricula`}
          >
            <ModuleConfigForm
              tenantId={company.tenantId}
              moduleKey={COMPANY_MODULE_KEYS.registration}
              label="Configuración de matrícula"
              onDirtyChange={(dirty) => setTabDirty("matricula", dirty)}
            />
          </div>
        )}

        {activeTab === "traspasos" && (
          <div
            role="tabpanel"
            id={`${tabsId}-panel-traspasos`}
            aria-labelledby={`${tabsId}-tab-traspasos`}
          >
            <ModuleConfigForm
              tenantId={company.tenantId}
              moduleKey={COMPANY_MODULE_KEYS.transfers}
              label="Configuración de traspasos"
              onDirtyChange={(dirty) => setTabDirty("traspasos", dirty)}
            />
          </div>
        )}

        {activeTab === "empresa" && (
          <div
            role="tabpanel"
            id={`${tabsId}-panel-empresa`}
            aria-labelledby={`${tabsId}-tab-empresa`}
            className="space-y-8"
          >
            <ModuleConfigForm
              tenantId={company.tenantId}
              moduleKey={COMPANY_MODULE_KEYS.company}
              label="Configuración general de empresa"
              onDirtyChange={setEmpresaModuleDirty}
            />
            <hr className="border-flit-draft/15" />
            <OtMatrixSection
              tenantId={company.tenantId}
              onDirtyChange={setEmpresaOtDirty}
            />
            <hr className="border-flit-draft/15" />
            <VehicleExceptionsSection tenantId={company.tenantId} />
          </div>
        )}

        {activeTab === "recaudo" && (
          <div
            role="tabpanel"
            id={`${tabsId}-panel-recaudo`}
            aria-labelledby={`${tabsId}-tab-recaudo`}
          >
            <ModuleConfigForm
              tenantId={company.tenantId}
              moduleKey={COMPANY_MODULE_KEYS.recaudo}
              label="Configuración de recaudo"
              onDirtyChange={(dirty) => setTabDirty("recaudo", dirty)}
            />
          </div>
        )}
      </div>
    </section>
  );
}
