import { useMemo, useState } from "react";
import type {
  AdminFormFieldItem,
  ProcedureTypeSummary,
} from "../api/parametrizacion.schemas.js";
import {
  useCreateFormField,
  useCreateFormSection,
  useEdgeForm,
  useProcedureMatrix,
} from "../api/parametrizacion.api.js";
import { useCanManageParametrizacion } from "../hooks/useCanManageParametrizacion.js";
import { parseApiFieldErrors, type FieldErrors } from "../lib/parseApiFieldErrors.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";

type FormEditorTabProps = {
  tenantId: string;
  selectedType: ProcedureTypeSummary | null;
};

const FIELD_TYPES = ["text", "select", "date"] as const;

export function FormEditorTab({ tenantId, selectedType }: FormEditorTabProps) {
  const canManage = useCanManageParametrizacion();
  const matrixQuery = useProcedureMatrix(
    selectedType ? { tenantId, typeCode: selectedType.code } : null,
    Boolean(selectedType),
  );

  const activeEdges = useMemo(
    () =>
      (matrixQuery.data?.edges ?? [])
        .filter((edge) => edge.isActive)
        .sort((a, b) => a.displayOrder - b.displayOrder),
    [matrixQuery.data],
  );

  const [selectedEdgeCode, setSelectedEdgeCode] = useState<string>("");
  const effectiveEdgeCode = selectedEdgeCode || activeEdges[0]?.code || "";

  const formQuery = useEdgeForm(
    selectedType && effectiveEdgeCode
      ? {
          tenantId,
          typeCode: selectedType.code,
          edgeCode: effectiveEdgeCode,
        }
      : null,
    Boolean(selectedType && effectiveEdgeCode),
  );

  const createSection = useCreateFormSection();
  const createField = useCreateFormField();

  const [sectionKey, setSectionKey] = useState("");
  const [sectionTitle, setSectionTitle] = useState("");
  const [fieldKey, setFieldKey] = useState("");
  const [fieldLabel, setFieldLabel] = useState("");
  const [fieldType, setFieldType] =
    useState<(typeof FIELD_TYPES)[number]>("text");
  const [fieldRequired, setFieldRequired] = useState(false);
  const [fieldOptionsJson, setFieldOptionsJson] = useState('["Opción A","Opción B"]');
  const [targetSectionId, setTargetSectionId] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  if (!selectedType) {
    return (
      <EmptyState
        title="Seleccione un tipo de trámite"
        description="Elija un tipo en Familias / Tipos para editar formularios dinámicos por paso."
      />
    );
  }

  if (matrixQuery.isLoading || formQuery.isLoading) {
    return <LoadingSkeleton rows={6} />;
  }

  const loadError = matrixQuery.error ?? formQuery.error;

  if (matrixQuery.isError || formQuery.isError) {
    return (
      <ErrorState
        error={loadError ?? new Error("Error al cargar formularios.")}
        onRetry={() => {
          void matrixQuery.refetch();
          void formQuery.refetch();
        }}
      />
    );
  }

  if (activeEdges.length === 0) {
    return (
      <EmptyState
        title="Sin pasos activos"
        description="Active al menos un paso en el pipeline antes de configurar formularios."
      />
    );
  }

  const sections = formQuery.data?.sections ?? [];
  const previewFields = sections.flatMap((section) => section.fields);

  async function handleCreateSection(event: React.FormEvent) {
    event.preventDefault();
    if (!selectedType || !effectiveEdgeCode) {
      return;
    }
    setFieldErrors({});
    try {
      await createSection.mutateAsync({
        params: {
          tenantId,
          typeCode: selectedType.code,
          edgeCode: effectiveEdgeCode,
        },
        payload: {
          sectionKey: sectionKey.trim(),
          title: sectionTitle.trim(),
          displayOrder: sections.length + 1,
          uiMode: "interactive",
        },
      });
      setSectionKey("");
      setSectionTitle("");
      setStatusMessage("Sección creada.");
    } catch (error) {
      setFieldErrors(parseApiFieldErrors(error).fields);
    }
  }

  async function handleCreateField(event: React.FormEvent) {
    event.preventDefault();
    if (!selectedType || !targetSectionId) {
      setFieldErrors({ sectionKey: "Seleccione una sección destino." });
      return;
    }
    setFieldErrors({});
    try {
      await createField.mutateAsync({
        params: {
          tenantId,
          typeCode: selectedType.code,
          sectionId: targetSectionId,
          edgeCode: effectiveEdgeCode,
        },
        payload: {
          fieldKey: fieldKey.trim(),
          dataType: fieldType,
          label: fieldLabel.trim(),
          isRequired: fieldRequired,
          displayOrder: previewFields.length + 1,
          uiState: "visible",
          isTrigger: false,
          optionsJson: fieldType === "select" ? fieldOptionsJson : null,
          validationJson: null,
        },
      });
      setFieldKey("");
      setFieldLabel("");
      setStatusMessage("Campo agregado.");
    } catch (error) {
      setFieldErrors(parseApiFieldErrors(error).fields);
    }
  }

  return (
    <div className="space-y-6">
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
          htmlFor="form-edge-select"
          className="text-sm font-semibold text-flit-blueDark"
        >
          Paso del pipeline
        </label>
        <select
          id="form-edge-select"
          value={effectiveEdgeCode}
          onChange={(event) => setSelectedEdgeCode(event.target.value)}
          className="mt-1 w-full max-w-md rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
        >
          {activeEdges.map((edge) => (
            <option key={edge.code} value={edge.code}>
              {edge.name} ({edge.code})
            </option>
          ))}
        </select>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card">
          <h2 className="text-base font-bold text-flit-blueText">Editor</h2>

          {canManage && (
            <>
              <form className="mt-4 space-y-3" onSubmit={handleCreateSection} noValidate>
                <p className="text-xs font-semibold uppercase tracking-wide text-flit-muted">
                  Nueva sección
                </p>
                <FieldInput
                  id="section-key"
                  label="Clave sección"
                  value={sectionKey}
                  error={fieldErrors.sectionKey}
                  onChange={setSectionKey}
                />
                <FieldInput
                  id="section-title"
                  label="Título"
                  value={sectionTitle}
                  error={fieldErrors.title}
                  onChange={setSectionTitle}
                />
                <GradientButton
                  type="submit"
                  className="!h-9 !w-auto !px-5 !text-xs"
                  disabled={createSection.isPending}
                >
                  Agregar sección
                </GradientButton>
              </form>

              <form className="mt-6 space-y-3 border-t border-flit-draft/10 pt-4" onSubmit={handleCreateField} noValidate>
                <p className="text-xs font-semibold uppercase tracking-wide text-flit-muted">
                  Nuevo campo
                </p>
                <div>
                  <label htmlFor="field-section" className="text-sm font-semibold text-flit-blueDark">
                    Sección
                  </label>
                  <select
                    id="field-section"
                    value={targetSectionId}
                    onChange={(event) => setTargetSectionId(event.target.value)}
                    className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
                  >
                    <option value="">Seleccione…</option>
                    {sections.map((section) => (
                      <option key={section.id} value={section.id}>
                        {section.title}
                      </option>
                    ))}
                  </select>
                </div>
                <FieldInput
                  id="field-key"
                  label="Clave campo"
                  value={fieldKey}
                  error={fieldErrors.fieldKey}
                  onChange={setFieldKey}
                />
                <FieldInput
                  id="field-label"
                  label="Etiqueta"
                  value={fieldLabel}
                  error={fieldErrors.label}
                  onChange={setFieldLabel}
                />
                <div>
                  <label htmlFor="field-type" className="text-sm font-semibold text-flit-blueDark">
                    Tipo
                  </label>
                  <select
                    id="field-type"
                    value={fieldType}
                    onChange={(event) =>
                      setFieldType(event.target.value as (typeof FIELD_TYPES)[number])
                    }
                    className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
                  >
                    {FIELD_TYPES.map((type) => (
                      <option key={type} value={type}>
                        {type}
                      </option>
                    ))}
                  </select>
                </div>
                {fieldType === "select" && (
                  <div>
                    <label htmlFor="field-options" className="text-sm font-semibold text-flit-blueDark">
                      Options JSON
                    </label>
                    <textarea
                      id="field-options"
                      value={fieldOptionsJson}
                      onChange={(event) => setFieldOptionsJson(event.target.value)}
                      rows={3}
                      className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 font-mono text-xs"
                    />
                  </div>
                )}
                <label className="inline-flex items-center gap-2 text-sm text-flit-blueDark">
                  <input
                    type="checkbox"
                    checked={fieldRequired}
                    onChange={(event) => setFieldRequired(event.target.checked)}
                  />
                  Obligatorio
                </label>
                <GradientButton
                  type="submit"
                  className="!h-9 !w-auto !px-5 !text-xs"
                  disabled={createField.isPending}
                >
                  Agregar campo
                </GradientButton>
              </form>
            </>
          )}

          {sections.length === 0 && (
            <p className="mt-4 text-sm text-flit-muted">
              No hay secciones en este paso. Cree una sección para agregar campos.
            </p>
          )}
        </section>

        <section
          aria-labelledby="form-preview-heading"
          className="rounded-flit-card border border-flit-draft/20 bg-flit-card p-4 shadow-flit-card"
        >
          <h2 id="form-preview-heading" className="text-base font-bold text-flit-blueText">
            Vista previa
          </h2>
          {previewFields.length === 0 ? (
            <p className="mt-4 text-sm text-flit-muted">
              Agregue campos para ver la vista previa del formulario dinámico.
            </p>
          ) : (
            <div className="mt-4 space-y-4">
              {sections.map((section) => (
                <fieldset
                  key={section.id}
                  className="rounded-[10px] border border-flit-draft/15 p-3"
                >
                  <legend className="px-1 text-sm font-semibold text-flit-blueDark">
                    {section.title}
                  </legend>
                  <div className="mt-2 space-y-3">
                    {section.fields.map((field) => (
                      <FormFieldPreview key={field.id} field={field} />
                    ))}
                  </div>
                </fieldset>
              ))}
            </div>
          )}
          <details className="mt-4">
            <summary className="cursor-pointer text-xs font-semibold text-flit-blueText">
              JSON schema (backend)
            </summary>
            <pre className="mt-2 max-h-48 overflow-auto rounded-[8px] bg-flit-bg p-3 font-mono text-[11px] text-flit-blueDark">
              {JSON.stringify(
                sections.map((section) => ({
                  sectionKey: section.sectionKey,
                  title: section.title,
                  uiMode: section.uiMode,
                  fields: section.fields.map((field) => ({
                    fieldKey: field.fieldKey,
                    dataType: field.dataType,
                    label: field.label,
                    isRequired: field.isRequired,
                    uiState: field.uiState,
                    options: field.options,
                    validation: field.validation,
                  })),
                })),
                null,
                2,
              )}
            </pre>
          </details>
        </section>
      </div>
    </div>
  );
}

function FormFieldPreview({ field }: { field: AdminFormFieldItem }) {
  const options =
    field.dataType === "select" && Array.isArray(field.options)
      ? field.options.map(String)
      : [];

  return (
    <div>
      <label
        htmlFor={`preview-${field.fieldKey}`}
        className="text-sm font-semibold text-flit-blueDark"
      >
        {field.label}
        {field.isRequired ? " *" : ""}
      </label>
      {field.dataType === "select" ? (
        <select
          id={`preview-${field.fieldKey}`}
          disabled
          className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
        >
          <option value="">Seleccione…</option>
          {options.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      ) : (
        <input
          id={`preview-${field.fieldKey}`}
          type={field.dataType === "date" ? "date" : "text"}
          disabled
          className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
        />
      )}
    </div>
  );
}

type FieldInputProps = {
  id: string;
  label: string;
  value: string;
  error?: string;
  onChange: (value: string) => void;
};

function FieldInput({ id, label, value, error, onChange }: FieldInputProps) {
  return (
    <div>
      <label htmlFor={id} className="text-sm font-semibold text-flit-blueDark">
        {label}
      </label>
      <input
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-[10px] border border-flit-draft/30 px-3 py-2 text-sm"
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
      />
      {error && (
        <p id={`${id}-error`} className="mt-1 text-xs text-flit-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
