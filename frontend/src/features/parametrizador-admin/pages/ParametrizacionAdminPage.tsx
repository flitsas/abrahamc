import { useMemo, useState } from "react";
import { useAuthMe } from "../../auth/api/auth.api.js";
import type { ProcedureTypeSummary } from "../api/parametrizacion.schemas.js";
import { useProcedureTypes } from "../api/parametrizacion.api.js";
import { FamiliesTypesTab } from "../components/FamiliesTypesTab.js";
import { FormEditorTab } from "../components/FormEditorTab.js";
import { ParametrizacionSubNav } from "../components/ParametrizacionSubNav.js";
import { PipelineModelerTab } from "../components/PipelineModelerTab.js";
import { RulesSimulatorTab } from "../components/RulesSimulatorTab.js";
import {
  buildParametrizacionSubNavItems,
  type ParametrizacionSubTabId,
} from "../lib/parametrizacionSubNav.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

export function ParametrizacionAdminPage() {
  const { data: session, isLoading, isError, error, refetch } = useAuthMe();
  const tenantId = session?.user.tenantId;
  const [activeTab, setActiveTab] =
    useState<ParametrizacionSubTabId>("familias-tipos");
  const [selectedType, setSelectedType] = useState<ProcedureTypeSummary | null>(
    null,
  );

  const typesQuery = useProcedureTypes(tenantId);
  const subNavItems = useMemo(() => buildParametrizacionSubNavItems(), []);

  function handleTypeCreated(code: string) {
    void typesQuery.refetch().then((result) => {
      const created = result.data?.items.find((type) => type.code === code);
      if (created) {
        setSelectedType(created);
        setActiveTab("pipeline");
      }
    });
  }

  if (isLoading) {
    return <LoadingSkeleton rows={4} />;
  }

  if (isError) {
    return <ErrorState error={error} onRetry={() => refetch()} />;
  }

  if (!tenantId) {
    return (
      <ErrorState
        error={new Error("Sesión sin tenantId. Inicie sesión nuevamente.")}
        onRetry={() => refetch()}
      />
    );
  }

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Parametrización low-code"
        subtitle="Configure familias, pipelines, formularios dinámicos y simule reglas de negocio por tipo de trámite."
      />

      <ParametrizacionSubNav
        items={subNavItems}
        activeId={activeTab}
        onChange={(id) => setActiveTab(id as ParametrizacionSubTabId)}
      />

      {activeTab === "familias-tipos" && (
        <FamiliesTypesTab
          tenantId={tenantId}
          selectedTypeCode={selectedType?.code ?? null}
          onSelectType={setSelectedType}
        />
      )}

      {activeTab === "pipeline" && (
        <PipelineModelerTab
          tenantId={tenantId}
          selectedType={selectedType}
          onTypeCreated={handleTypeCreated}
        />
      )}

      {activeTab === "formularios" && (
        <FormEditorTab tenantId={tenantId} selectedType={selectedType} />
      )}

      {activeTab === "reglas" && (
        <RulesSimulatorTab tenantId={tenantId} selectedType={selectedType} />
      )}
    </div>
  );
}
