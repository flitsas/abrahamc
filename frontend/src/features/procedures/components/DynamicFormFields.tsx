import { TextField } from "../../../shared/components/flit/TextField.js";
import type { FormSectionConfig } from "../api/procedure-wizard.schemas.js";

type DynamicFormFieldsProps = {
  sections: FormSectionConfig[];
  values: Record<string, string>;
  errors: Record<string, string>;
  onChange: (fieldKey: string, value: string) => void;
  readOnly?: boolean;
};

export function DynamicFormFields({
  sections,
  values,
  errors,
  onChange,
  readOnly = false,
}: DynamicFormFieldsProps) {
  const interactiveSections = sections.filter(
    (section) => section.uiMode !== "read_only",
  );

  if (interactiveSections.length === 0) {
    return (
      <p className="text-sm text-flit-muted">
        Este paso no requiere captura de datos adicionales.
      </p>
    );
  }

  return (
    <div className="space-y-6">
      {interactiveSections.map((section) => (
        <fieldset
          key={section.sectionKey}
          className="rounded-[10px] border border-flit-draft/15 p-4"
        >
          <legend className="px-1 text-sm font-semibold text-flit-blueDark">
            {section.title}
          </legend>
          <div className="mt-3 space-y-4">
            {[...section.fields]
              .sort((a, b) => a.displayOrder - b.displayOrder)
              .map((field) => {
                if (field.uiState === "oculto") {
                  return null;
                }

                const fieldId = `field-${field.fieldKey}`;
                const value = values[field.fieldKey] ?? "";
                const error = errors[field.fieldKey];

                if (field.dataType === "select") {
                  const options = Array.isArray(field.options)
                    ? field.options.map(String)
                    : [];

                  return (
                    <div key={field.fieldKey}>
                      <label
                        htmlFor={fieldId}
                        className="block text-sm font-semibold text-flit-blueDark"
                      >
                        {field.label}
                        {field.isRequired ? " *" : ""}
                      </label>
                      <select
                        id={fieldId}
                        value={value}
                        disabled={readOnly}
                        onChange={(event) =>
                          onChange(field.fieldKey, event.target.value)
                        }
                        aria-invalid={Boolean(error)}
                        aria-describedby={
                          error ? `${fieldId}-error` : undefined
                        }
                        className="mt-2 h-12 w-full rounded-[10px] border border-flit-border bg-white px-4 text-sm text-flit-blueDark focus:border-flit-blue focus:outline-none focus:ring-2 focus:ring-flit-blue/20 disabled:opacity-60"
                      >
                        <option value="">Seleccione…</option>
                        {options.map((option) => (
                          <option key={option} value={option}>
                            {option}
                          </option>
                        ))}
                      </select>
                      {error && (
                        <p
                          id={`${fieldId}-error`}
                          className="mt-1 text-xs text-flit-danger"
                          role="alert"
                        >
                          {error}
                        </p>
                      )}
                    </div>
                  );
                }

                const inputType =
                  field.dataType === "number"
                    ? "number"
                    : field.dataType === "date"
                      ? "date"
                      : "text";

                return (
                  <div key={field.fieldKey}>
                    <TextField
                      id={fieldId}
                      name={field.fieldKey}
                      label={`${field.label}${field.isRequired ? " *" : ""}`}
                      type={inputType}
                      value={value}
                      disabled={readOnly}
                      onChange={(event) =>
                        onChange(field.fieldKey, event.target.value)
                      }
                      aria-invalid={Boolean(error)}
                      aria-describedby={error ? `${fieldId}-error` : undefined}
                    />
                    {error && (
                      <p
                        id={`${fieldId}-error`}
                        className="mt-1 text-xs text-flit-danger"
                        role="alert"
                      >
                        {error}
                      </p>
                    )}
                  </div>
                );
              })}
          </div>
        </fieldset>
      ))}
    </div>
  );
}
