import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { DashboardTab } from "../components/DashboardTab.js";
import { DocumentOrderTab } from "../components/DocumentOrderTab.js";
import { IntegrationLogsTab } from "../components/IntegrationLogsTab.js";
import { OtAgencySelector } from "../components/OtAgencySelector.js";
import { OtTramitesSubNav } from "../components/OtTramitesSubNav.js";
import { useOtAgency } from "../api/ot-admin.api.js";
import {
  buildOtTramitesSubNavItems,
  type OtTramitesSubTabId,
} from "../lib/otTramitesSubNav.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

export function OtTramitesConsolePage() {
  const { agencyId } = useParams<{ agencyId?: string }>();
  const [activeTab, setActiveTab] = useState<OtTramitesSubTabId>("dashboard");
  const agencyQuery = useOtAgency(agencyId);
  const subNavItems = useMemo(() => buildOtTramitesSubNavItems(), []);

  if (!agencyId) {
    return (
      <div className="space-y-6">
        <PageHeaderCard
          title="Consola OT — Trámites"
          subtitle="Seleccione un organismo de tránsito para operar la consola unificada."
        />
        <OtAgencySelector />
      </div>
    );
  }

  if (agencyQuery.isLoading) {
    return <LoadingSkeleton rows={4} />;
  }

  if (agencyQuery.isError) {
    return (
      <ErrorState
        error={agencyQuery.error}
        onRetry={() => agencyQuery.refetch()}
      />
    );
  }

  const agency = agencyQuery.data;

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Consola OT — Trámites"
        subtitle={
          agency
            ? `${agency.name} (${agency.code}) — Dashboard unificado, orden consolidado y bitácora de integración.`
            : "Operación de trámites por organismo de tránsito."
        }
        actions={
          <Link
            to="/admin/ot/tramites"
            className="inline-flex h-11 items-center rounded-flit-pill border border-flit-border px-5 text-sm font-semibold text-flit-blueText transition hover:bg-flit-bg focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          >
            Cambiar OT
          </Link>
        }
      />

      <OtTramitesSubNav
        items={subNavItems}
        activeId={activeTab}
        onChange={(id) => setActiveTab(id as OtTramitesSubTabId)}
      />

      {activeTab === "dashboard" && <DashboardTab agencyId={agencyId} />}
      {activeTab === "orden-documentos" && (
        <DocumentOrderTab agencyId={agencyId} />
      )}
      {activeTab === "logs-integracion" && (
        <IntegrationLogsTab agencyId={agencyId} />
      )}
    </div>
  );
}
