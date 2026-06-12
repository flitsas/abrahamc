import { useState } from "react";
import type {
  CatalogFamily,
  ProcedureTypeSummary,
} from "../api/parametrizacion.schemas.js";
import {
  useCatalogFamilies,
  useCreateCatalogFamily,
  useDeleteCatalogFamily,
  useProcedureTypes,
} from "../api/parametrizacion.api.js";
import { useCanManageParametrizacion } from "../hooks/useCanManageParametrizacion.js";
import {
  parseApiFieldErrors,
  type FieldErrors,
} from "../lib/parseApiFieldErrors.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type FamiliesTypesTabProps = {
  tenantId: string;
  selectedTypeCode: string | null;
  onSelectType: (type: ProcedureTypeSummary | null) => void;
};

export function FamiliesTypesTab({
  tenantId,
  selectedTypeCode,
  onSelectType,
}: FamiliesTypesTabProps) {
  const canManage = useCanManageParametrizacion();
  const familiesQuery = useCatalogFamilies();
  const typesQuery = useProcedureTypes(tenantId);
  const createFamily = useCreateCatalogFamily();
  const deleteFamily = useDeleteCatalogFamily();

  const [familyCode, setFamilyCode] = useState("");
  const [familyName, setFamilyName] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const isLoading = familiesQuery.isLoading || typesQuery.isLoading;
  const isError = familiesQuery.isError || typesQuery.isError;

  async function handleCreateFamily(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    try {
      await createFamily.mutateAsync({
        code: familyCode.trim().toUpperCase(),
        name: familyName.trim(),
      });
      setFamilyCode("");
      setFamilyName("");
    } catch (createError) {
      const parsed = parseApiFieldErrors(createError);
      setFieldErrors(parsed.fields);
    }
  }

  async function handleDeleteFamily(family: CatalogFamily) {
    if (!window.confirm(`¿Eliminar familia «${family.name}»?`)) {
      return;
    }
    try {
      await deleteFamily.mutateAsync(family.code);
      if (selectedTypeCode) {
        const stillExists = typesQuery.data?.items.some(
          (type) => type.code === selectedTypeCode,
        );
        if (!stillExists) {
          onSelectType(null);
        }
      }
    } catch {
      // mutation error handled by UI if needed
    }
  }

  if (isLoading) {
    return <LoadingSkeleton rows={5} />;
  }

  const loadError = familiesQuery.error ?? typesQuery.error;

  if (isError && loadError) {
    return (
      <ErrorState
        error={loadError}
        onRetry={() => {
          void familiesQuery.refetch();
          void typesQuery.refetch();
        }}
      />
    );
  }

  const families = familiesQuery.data?.items ?? [];
  const types = typesQuery.data?.items ?? [];

  if (families.length === 0 && types.length === 0) {
    return (
      <div className="space-y-6">
        <EmptyState
          title="Sin catálogo de parametrización"
          description="Cree una familia de trámites para comenzar a definir tipos y pipelines."
        />
        {canManage && (
          <FamilyForm
            familyCode={familyCode}
            familyName={familyName}
            fieldErrors={fieldErrors}
            isPending={createFamily.isPending}
            onCodeChange={setFamilyCode}
            onNameChange={setFamilyName}
            onSubmit={handleCreateFamily}
          />
        )}
      </div>
    );
  }

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <section
        aria-labelledby="families-heading"
        className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card"
      >
        <h2
          id="families-heading"
          className="text-base font-bold text-flit-blueText"
        >
          Familias
        </h2>
        <ul className="mt-4 divide-y divide-flit-draft/10">
          {families.map((family) => (
            <li
              key={family.id}
              className="flex items-center justify-between py-3 text-sm"
            >
              <div>
                <p className="font-semibold text-flit-blueDark">
                  {family.name}
                </p>
                <p className="font-mono text-xs text-flit-muted">
                  {family.code}
                </p>
              </div>
              {canManage && (
                <button
                  type="button"
                  onClick={() => handleDeleteFamily(family)}
                  className="text-xs font-semibold text-flit-danger hover:underline"
                  aria-label={`Eliminar familia ${family.name}`}
                >
                  Eliminar
                </button>
              )}
            </li>
          ))}
        </ul>
        {canManage && (
          <div className="mt-4 border-t border-flit-draft/10 pt-4">
            <FamilyForm
              familyCode={familyCode}
              familyName={familyName}
              fieldErrors={fieldErrors}
              isPending={createFamily.isPending}
              onCodeChange={setFamilyCode}
              onNameChange={setFamilyName}
              onSubmit={handleCreateFamily}
            />
          </div>
        )}
      </section>

      <section
        aria-labelledby="types-heading"
        className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card"
      >
        <h2
          id="types-heading"
          className="text-base font-bold text-flit-blueText"
        >
          Tipos de trámite
        </h2>
        {types.length === 0 ? (
          <p className="mt-4 text-sm text-flit-muted">
            No hay tipos configurados para este tenant. Use la pestaña Pipeline
            para crear uno.
          </p>
        ) : (
          <ul className="mt-4 divide-y divide-flit-draft/10">
            {types.map((type) => {
              const selected = selectedTypeCode === type.code;
              return (
                <li key={type.id}>
                  <button
                    type="button"
                    onClick={() => onSelectType(selected ? null : type)}
                    aria-pressed={selected}
                    className={`flex w-full items-start justify-between py-3 text-left text-sm transition ${
                      selected ? "bg-flit-blue/5 px-2 rounded-[8px]" : ""
                    }`}
                  >
                    <div>
                      <p className="font-semibold text-flit-blueDark">
                        {type.name}
                      </p>
                      <p className="font-mono text-xs text-flit-muted">
                        {type.code} · {type.familyCode}
                      </p>
                    </div>
                    <span className="text-xs text-flit-muted">
                      {type.activeEdgeCount}/{type.maxSteps} pasos
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </div>
  );
}

type FamilyFormProps = {
  familyCode: string;
  familyName: string;
  fieldErrors: FieldErrors;
  isPending: boolean;
  onCodeChange: (value: string) => void;
  onNameChange: (value: string) => void;
  onSubmit: (event: React.FormEvent) => void;
};

function FamilyForm({
  familyCode,
  familyName,
  fieldErrors,
  isPending,
  onCodeChange,
  onNameChange,
  onSubmit,
}: FamilyFormProps) {
  return (
    <form className="space-y-3" onSubmit={onSubmit} noValidate>
      <div>
        <label
          htmlFor="family-code"
          className="text-sm font-semibold text-flit-blueDark"
        >
          Código
        </label>
        <input
          id="family-code"
          name="code"
          value={familyCode}
          onChange={(event) => onCodeChange(event.target.value)}
          className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
          aria-invalid={Boolean(fieldErrors.code)}
          aria-describedby={fieldErrors.code ? "family-code-error" : undefined}
        />
        {fieldErrors.code && (
          <p
            id="family-code-error"
            className="mt-1 text-xs text-flit-danger"
            role="alert"
          >
            {fieldErrors.code}
          </p>
        )}
      </div>
      <div>
        <label
          htmlFor="family-name"
          className="text-sm font-semibold text-flit-blueDark"
        >
          Nombre
        </label>
        <input
          id="family-name"
          name="name"
          value={familyName}
          onChange={(event) => onNameChange(event.target.value)}
          className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
          aria-invalid={Boolean(fieldErrors.name)}
          aria-describedby={fieldErrors.name ? "family-name-error" : undefined}
        />
        {fieldErrors.name && (
          <p
            id="family-name-error"
            className="mt-1 text-xs text-flit-danger"
            role="alert"
          >
            {fieldErrors.name}
          </p>
        )}
      </div>
      <GradientButton
        type="submit"
        className="!h-10 !w-auto !px-6 !text-sm"
        disabled={isPending}
      >
        {isPending ? "Guardando…" : "Agregar familia"}
      </GradientButton>
    </form>
  );
}
