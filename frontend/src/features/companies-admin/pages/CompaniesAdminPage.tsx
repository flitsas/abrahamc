import { useEffect, useState } from "react";
import { PERMISSIONS } from "../../auth/lib/permissions.js";
import { usePermission } from "../../auth/hooks/usePermission.js";
import { useCompaniesList } from "../api/companies.api.js";
import type { CompanyRow } from "../api/companies.schemas.js";
import { CompanyConfigTabs } from "../components/CompanyConfigTabs.js";
import { CompanyGrid } from "../components/CompanyGrid.js";
import { CreateCompanyDialog } from "../components/CreateCompanyDialog.js";
import { useDebouncedValue } from "../hooks/useDebouncedValue.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";

const PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 300;

export function CompaniesAdminPage() {
  const canManageCompanies = usePermission(PERMISSIONS.manageCompanies);
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const debouncedSearch = useDebouncedValue(
    searchInput.trim(),
    SEARCH_DEBOUNCE_MS,
  );
  const [selectedCompany, setSelectedCompany] = useState<CompanyRow | null>(
    null,
  );
  const [createOpen, setCreateOpen] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const companiesQuery = useCompaniesList({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
  });

  useEffect(() => {
    if (!selectedCompany || !companiesQuery.data?.data) {
      return;
    }
    const refreshed = companiesQuery.data.data.find(
      (row) => row.id === selectedCompany.id,
    );
    if (
      refreshed &&
      (refreshed.legalName !== selectedCompany.legalName ||
        refreshed.commercialName !== selectedCompany.commercialName ||
        refreshed.updatedAt !== selectedCompany.updatedAt)
    ) {
      setSelectedCompany(refreshed);
    }
  }, [companiesQuery.data, selectedCompany]);

  function handleSearchChange(value: string) {
    setSearchInput(value);
    setPage(1);
  }

  function handleSelectCompany(company: CompanyRow) {
    setSelectedCompany((current) =>
      current?.tenantId === company.tenantId ? null : company,
    );
  }

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title="Compañías B2B"
        subtitle="Indexación y configuración de módulos por tenant para operación SuperAdmin."
        actions={
          canManageCompanies ? (
            <GradientButton
              type="button"
              className="!h-12 !w-auto !px-8 !text-sm"
              onClick={() => setCreateOpen(true)}
            >
              Nueva compañía
            </GradientButton>
          ) : undefined
        }
      />

      {statusMessage && (
        <div
          className="rounded-[10px] border border-flit-green/30 bg-flit-green/10 px-4 py-3 text-sm text-flit-green"
          role="status"
        >
          {statusMessage}
        </div>
      )}

      <div>
        <label
          htmlFor="company-search"
          className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
        >
          Buscar compañía
        </label>
        <input
          id="company-search"
          type="search"
          value={searchInput}
          onChange={(event) => handleSearchChange(event.target.value)}
          placeholder="NIT, razón social o nombre comercial…"
          className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 text-sm text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
        />
        <p className="mt-1.5 text-xs text-flit-muted">
          Búsqueda con debounce de {SEARCH_DEBOUNCE_MS} ms
        </p>
      </div>

      <CompanyGrid
        companies={companiesQuery.data?.data}
        totalCount={companiesQuery.data?.totalCount ?? 0}
        page={page}
        pageSize={PAGE_SIZE}
        isLoading={companiesQuery.isLoading}
        isError={companiesQuery.isError}
        error={companiesQuery.error}
        selectedTenantId={selectedCompany?.tenantId ?? null}
        onSelect={handleSelectCompany}
        onPageChange={setPage}
        onRetry={() => companiesQuery.refetch()}
      />

      {selectedCompany && (
        <CompanyConfigTabs
          company={selectedCompany}
          onCompanyUpdated={() => companiesQuery.refetch()}
        />
      )}

      <CreateCompanyDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSuccess={(legalName) => {
          setStatusMessage(`Compañía «${legalName}» creada correctamente.`);
          setPage(1);
        }}
      />
    </div>
  );
}
