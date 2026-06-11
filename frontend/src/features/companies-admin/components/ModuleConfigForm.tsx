import { useEffect, useState } from "react";
import {
  useCompanyModuleConfig,
  useUpsertModuleConfig,
} from "../api/companies.api.js";
import type { CompanyModuleKey } from "../api/companies.schemas.js";
import { useCanEditCompanyConfig } from "../hooks/useCanEditCompanyConfig.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type ModuleConfigFormProps = {
  tenantId: string;
  moduleKey: CompanyModuleKey;
  label: string;
  onDirtyChange: (dirty: boolean) => void;
  onSaved?: () => void;
};

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function isValidJson(value: string): boolean {
  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}

export function ModuleConfigForm({
  tenantId,
  moduleKey,
  label,
  onDirtyChange,
  onSaved,
}: ModuleConfigFormProps) {
  const canEdit = useCanEditCompanyConfig();
  const configQuery = useCompanyModuleConfig(tenantId, moduleKey);
  const saveMutation = useUpsertModuleConfig(tenantId, moduleKey);
  const [configJson, setConfigJson] = useState("{}");
  const [isActive, setIsActive] = useState(true);
  const [savedSnapshot, setSavedSnapshot] = useState("{}");
  const [savedActive, setSavedActive] = useState(true);
  const [parseError, setParseError] = useState<string | null>(null);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);

  useEffect(() => {
    if (configQuery.isLoading) {
      return;
    }
    const loadedJson = configQuery.data?.configJson ?? "{}";
    const loadedActive = configQuery.data?.isActive ?? true;
    const formatted = formatJson(loadedJson);
    setConfigJson(formatted);
    setSavedSnapshot(formatted);
    setIsActive(loadedActive);
    setSavedActive(loadedActive);
    setParseError(null);
  }, [configQuery.data, configQuery.isLoading, tenantId, moduleKey]);

  const isDirty =
    configJson !== savedSnapshot || isActive !== savedActive;

  useEffect(() => {
    onDirtyChange(isDirty);
  }, [isDirty, onDirtyChange]);

  function handleJsonChange(value: string) {
    setConfigJson(value);
    setParseError(isValidJson(value) ? null : "JSON inválido.");
    setSaveMessage(null);
  }

  async function handleSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canEdit || !isValidJson(configJson)) {
      setParseError("JSON inválido.");
      return;
    }

    const normalized = formatJson(configJson);
    await saveMutation.mutateAsync({
      configJson: normalized,
      isActive,
    });
    setConfigJson(normalized);
    setSavedSnapshot(normalized);
    setSavedActive(isActive);
    setSaveMessage("Configuración guardada.");
    onSaved?.();
  }

  if (configQuery.isLoading) {
    return <LoadingSkeleton rows={4} />;
  }

  if (configQuery.isError) {
    return (
      <ErrorState error={configQuery.error} onRetry={() => configQuery.refetch()} />
    );
  }

  return (
    <form className="space-y-4" onSubmit={handleSave}>
      <div className="flex items-center justify-between gap-4">
        <h3 className="text-base font-semibold text-flit-blueDark">{label}</h3>
        <label className="flex items-center gap-2 text-sm text-flit-blueDark">
          <input
            type="checkbox"
            checked={isActive}
            disabled={!canEdit}
            onChange={(event) => setIsActive(event.target.checked)}
            className="h-4 w-4 rounded border-flit-draft/40 text-flit-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          />
          Módulo activo
        </label>
      </div>

      <div>
        <label
          htmlFor={`module-config-${moduleKey}`}
          className="mb-1.5 block text-sm font-semibold text-flit-blueDark"
        >
          Configuración JSON
        </label>
        <textarea
          id={`module-config-${moduleKey}`}
          value={configJson}
          readOnly={!canEdit}
          onChange={(event) => handleJsonChange(event.target.value)}
          rows={12}
          spellCheck={false}
          aria-invalid={parseError ? true : undefined}
          aria-describedby={
            parseError ? `module-config-error-${moduleKey}` : undefined
          }
          className="w-full rounded-[10px] border border-flit-draft/30 bg-white px-4 py-3 font-mono text-xs text-flit-blueDark focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue read-only:bg-flit-bg read-only:text-flit-muted"
        />
        {parseError && (
          <p
            id={`module-config-error-${moduleKey}`}
            className="mt-1.5 text-sm text-flit-danger"
            role="alert"
          >
            {parseError}
          </p>
        )}
      </div>

      {saveMessage && (
        <p className="text-sm text-flit-green" role="status">
          {saveMessage}
        </p>
      )}

      {saveMutation.isError && (
        <p className="text-sm text-flit-danger" role="alert">
          {saveMutation.error instanceof Error
            ? saveMutation.error.message
            : "No se pudo guardar la configuración."}
        </p>
      )}

      {canEdit && (
        <GradientButton
          type="submit"
          className="!h-11 !w-auto !px-8 !text-sm"
          disabled={
            !isDirty ||
            Boolean(parseError) ||
            saveMutation.isPending
          }
        >
          {saveMutation.isPending ? "Guardando…" : "Guardar configuración"}
        </GradientButton>
      )}
    </form>
  );
}
