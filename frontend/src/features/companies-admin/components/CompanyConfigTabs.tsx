import { useCallback, useEffect, useId, useMemo, useState } from "react";
import type { CompanyRow } from "../api/companies.schemas.js";
import { COMPANY_MODULE_KEYS } from "../api/companies.schemas.js";
import {
  buildEmpresaSubNavItems,
  type EmpresaSubTabId,
} from "../lib/empresaSubNav.js";
import { useCanEditCompanyConfig } from "../hooks/useCanEditCompanyConfig.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { CompanyConfigSubNav } from "./CompanyConfigSubNav.js";
import { EditCompanyDialog } from "./EditCompanyDialog.js";
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
  onCompanyUpdated?: () => void;
};

export function CompanyConfigTabs({
  company,
  onCompanyUpdated,
}: CompanyConfigTabsProps) {
  const canEdit = useCanEditCompanyConfig();
  const tabsId = useId();
  const [editOpen, setEditOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<ConfigTabId>("matricula");
  const [dirtyTabs, setDirtyTabs] = useState<Partial<Record<ConfigTabId, boolean>>>(
    {},
  );
  const [activeEmpresaSubTab, setActiveEmpresaSubTab] =
    useState<EmpresaSubTabId>("general");
  const [empresaGeneralDirty, setEmpresaGeneralDirty] = useState(false);
  const [empresaOtDirty, setEmpresaOtDirty] = useState(false);

  const empresaSubNavItems = useMemo(
    () =>
      buildEmpresaSubNavItems({
        general: empresaGeneralDirty,
        "matriz-ot": empresaOtDirty,
      }),
    [empresaGeneralDirty, empresaOtDirty],
  );

  useEffect(() => {
    setActiveEmpresaSubTab("general");
    setEmpresaGeneralDirty(false);
    setEmpresaOtDirty(false);
  }, [company.tenantId]);

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
      const combined = empresaGeneralDirty || empresaOtDirty;
      if (current.empresa === combined) {
        return current;
      }
      return { ...current, empresa: combined };
    });
  }, [empresaGeneralDirty, empresaOtDirty]);

  function isEmpresaSubTabDirty(subTab: EmpresaSubTabId): boolean {
    if (subTab === "general") {
      return empresaGeneralDirty;
    }
    if (subTab === "matriz-ot") {
      return empresaOtDirty;
    }
    return false;
  }

  function clearEmpresaSubTabDirty(subTab: EmpresaSubTabId) {
    if (subTab === "general") {
      setEmpresaGeneralDirty(false);
      return;
    }
    if (subTab === "matriz-ot") {
      setEmpresaOtDirty(false);
    }
  }

  function requestEmpresaSubTabChange(nextSubTab: EmpresaSubTabId) {
    if (nextSubTab === activeEmpresaSubTab) {
      return;
    }

    if (isEmpresaSubTabDirty(activeEmpresaSubTab)) {
      const confirmed = window.confirm(
        "Hay cambios sin guardar en este submódulo. ¿Desea cambiar sin guardar?",
      );
      if (!confirmed) {
        return;
      }
      clearEmpresaSubTabDirty(activeEmpresaSubTab);
    }

    setActiveEmpresaSubTab(nextSubTab);
  }

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
        setEmpresaGeneralDirty(false);
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
      <header className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2
            id={`${tabsId}-heading`}
            className="text-lg font-bold text-flit-blueDark"
          >
            Configuración — {company.legalName}
          </h2>
          <p className="mt-1 text-sm text-flit-muted">
            NIT {company.nit} · Tenant {company.tenantName}
          </p>
        </div>
        {canEdit && (
          <GradientButton
            type="button"
            className="!h-12 !w-auto shrink-0 !px-8 !text-sm"
            onClick={() => setEditOpen(true)}
          >
            Editar datos
          </GradientButton>
        )}
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
            className="space-y-6"
          >
            <CompanyConfigSubNav
              idPrefix={tabsId}
              ariaLabel="Submódulos de empresa"
              items={empresaSubNavItems}
              activeId={activeEmpresaSubTab}
              onChange={(subTabId) =>
                requestEmpresaSubTabChange(subTabId as EmpresaSubTabId)
              }
            />

            {activeEmpresaSubTab === "general" && (
              <ModuleConfigForm
                tenantId={company.tenantId}
                moduleKey={COMPANY_MODULE_KEYS.company}
                label="Configuración general de empresa"
                embedded
                onDirtyChange={setEmpresaGeneralDirty}
              />
            )}

            {activeEmpresaSubTab === "matriz-ot" && (
              <OtMatrixSection
                tenantId={company.tenantId}
                embedded
                onDirtyChange={setEmpresaOtDirty}
              />
            )}

            {activeEmpresaSubTab === "excepciones" && (
              <VehicleExceptionsSection
                tenantId={company.tenantId}
                embedded
              />
            )}
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

      <EditCompanyDialog
        open={editOpen}
        company={company}
        onClose={() => setEditOpen(false)}
        onSuccess={() => onCompanyUpdated?.()}
      />
    </section>
  );
}
