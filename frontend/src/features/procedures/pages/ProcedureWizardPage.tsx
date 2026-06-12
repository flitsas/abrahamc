import { useId, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuthMe } from "../../auth/api/auth.api.js";
import { useProcedureInstance } from "../api/procedure-instances.api.js";
import {
  runProcedureQueriesAsync,
  useCreateProcedureInstance,
  useProcedureConfiguration,
  useSaveProcedureOwners,
  useValidateOwnership,
} from "../api/procedure-wizard.api.js";
import type { ProcedureOwner } from "../api/procedure-wizard.schemas.js";
import {
  CopropiedadStep,
  createEmptyOwner,
  sumOwnershipPercentages,
} from "../components/CopropiedadStep.js";
import { DynamicFormFields } from "../components/DynamicFormFields.js";
import { QueryBanners } from "../components/QueryBanners.js";
import { WizardStepper } from "../components/WizardStepper.js";
import { PageHeaderCard } from "../../../shared/components/flit/PageHeaderCard.js";
import { GradientButton } from "../../../shared/components/flit/GradientButton.js";
import { EmptyState } from "../../../shared/components/ui/EmptyState.js";
import { ErrorState } from "../../../shared/components/ui/ErrorState.js";
import { LoadingSkeleton } from "../../../shared/components/ui/LoadingSkeleton.js";
import { resolveProcedureTypeCode } from "../lib/instanceTypeCode.js";
import { buildWizardSteps, stepHasPlateTrigger } from "../lib/wizardSteps.js";

type WizardMode = "new" | "continue";

function validateStepFields(
  fieldValues: Record<string, string>,
  sections: ReturnType<typeof buildWizardSteps>[number]["sections"],
): Record<string, string> {
  const errors: Record<string, string> = {};

  for (const section of sections) {
    for (const field of section.fields) {
      if (field.uiState === "oculto" || section.uiMode === "read_only") {
        continue;
      }
      if (field.isRequired && !fieldValues[field.fieldKey]?.trim()) {
        errors[field.fieldKey] = "Campo obligatorio.";
      }
    }
  }

  return errors;
}

type ProcedureWizardPageProps = {
  mode: WizardMode;
};

export function ProcedureWizardPage({ mode }: ProcedureWizardPageProps) {
  const stepperId = useId();
  const navigate = useNavigate();
  const params = useParams();
  const typeCodeParam = mode === "new" ? params.typeCode : undefined;
  const instanceIdParam = mode === "continue" ? params.instanceId : undefined;

  const { data: session } = useAuthMe();
  const tenantId = session?.user.tenantId;
  const userId = session?.user.id;

  const instanceQuery = useProcedureInstance(instanceIdParam, tenantId);
  const resolvedTypeCode =
    typeCodeParam ??
    (instanceQuery.data
      ? resolveProcedureTypeCode(instanceQuery.data)
      : undefined);

  const configQuery = useProcedureConfiguration(resolvedTypeCode, tenantId);
  const createMutation = useCreateProcedureInstance();
  const saveOwnersMutation = useSaveProcedureOwners();

  const [instanceId, setInstanceId] = useState<string | undefined>(
    instanceIdParam,
  );
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [owners, setOwners] = useState<ProcedureOwner[]>([createEmptyOwner()]);
  const [queryPolling, setQueryPolling] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const steps = useMemo(
    () => (configQuery.data ? buildWizardSteps(configQuery.data) : []),
    [configQuery.data],
  );

  const currentStep = steps[currentStepIndex];
  const ownershipTotal = sumOwnershipPercentages(owners);
  const ownershipQuery = useValidateOwnership(
    instanceId,
    tenantId,
    Boolean(currentStep?.isCopropiedad && instanceId),
  );

  const ownershipValid = currentStep?.isCopropiedad
    ? (ownershipQuery.data?.ownershipValid ??
      Math.abs(ownershipTotal - 100) < 0.01)
    : true;

  const isBootstrapping =
    (mode === "continue" && instanceQuery.isLoading) ||
    configQuery.isLoading ||
    !tenantId ||
    !userId;

  const bootstrapError =
    (mode === "continue" && instanceQuery.isError && instanceQuery.error) ||
    (configQuery.isError && configQuery.error) ||
    null;

  function handleFieldChange(fieldKey: string, value: string) {
    setFieldValues((current) => ({ ...current, [fieldKey]: value }));
    setFieldErrors((current) => {
      if (!current[fieldKey]) {
        return current;
      }
      const next = { ...current };
      delete next[fieldKey];
      return next;
    });
  }

  async function ensureInstance(edgeCode: string): Promise<string | undefined> {
    if (instanceId) {
      return instanceId;
    }
    if (!tenantId || !userId || !resolvedTypeCode) {
      return undefined;
    }

    const created = await createMutation.mutateAsync({
      tenantId,
      filedByUserId: userId,
      procedureTypeCode: resolvedTypeCode,
      edgeCode,
      fieldValues: fieldValues,
    });
    setInstanceId(created.id);
    navigate(`/tramites/${created.id}`, { replace: true });
    return created.id;
  }

  async function triggerQueriesIfNeeded(
    activeInstanceId: string,
    edgeCode: string,
  ) {
    if (!tenantId || !userId || !resolvedTypeCode || !currentStep) {
      return;
    }
    if (!stepHasPlateTrigger(currentStep.sections)) {
      return;
    }

    await runProcedureQueriesAsync(activeInstanceId, {
      tenantId,
      executedByUserId: userId,
      procedureTypeCode: resolvedTypeCode,
      edgeCode,
      documentTypeCode: "CC",
      capturedFields: fieldValues,
    });
    setQueryPolling(true);
  }

  async function handleNext() {
    if (!currentStep || !tenantId || !userId) {
      return;
    }

    setSubmitError(null);

    if (currentStep.isCopropiedad) {
      if (!instanceId) {
        setSubmitError(
          "Debe radicar el trámite antes de registrar copropiedad.",
        );
        return;
      }
      if (!ownershipValid) {
        setSubmitError("La suma de porcentajes debe ser 100 %.");
        return;
      }
      try {
        await saveOwnersMutation.mutateAsync({
          instanceId,
          request: { tenantId, owners },
        });
      } catch {
        setSubmitError("No se pudo guardar la copropiedad.");
        return;
      }
    } else {
      const errors = validateStepFields(fieldValues, currentStep.sections);
      if (Object.keys(errors).length > 0) {
        setFieldErrors(errors);
        return;
      }

      try {
        const activeInstanceId = await ensureInstance(currentStep.edge.code);
        if (!activeInstanceId) {
          setSubmitError("No se pudo crear la instancia del trámite.");
          return;
        }
        await triggerQueriesIfNeeded(activeInstanceId, currentStep.edge.code);
      } catch {
        setSubmitError("No se pudo avanzar el trámite.");
        return;
      }
    }

    if (currentStepIndex < steps.length - 1) {
      setCurrentStepIndex((index) => index + 1);
    }
  }

  function handleBack() {
    setSubmitError(null);
    setCurrentStepIndex((index) => Math.max(0, index - 1));
  }

  if (isBootstrapping) {
    return <LoadingSkeleton rows={8} />;
  }

  if (bootstrapError) {
    return (
      <ErrorState
        error={bootstrapError}
        onRetry={() => {
          if (mode === "continue") {
            void instanceQuery.refetch();
          }
          void configQuery.refetch();
        }}
      />
    );
  }

  if (!configQuery.data || steps.length === 0) {
    return (
      <div className="space-y-4">
        <EmptyState
          title="Sin pasos configurados"
          description="Este tipo de trámite no tiene aristas activas en la configuración."
        />
        <Link
          to="/tramites"
          className="text-sm font-semibold text-flit-blue underline"
        >
          Volver a trámites
        </Link>
      </div>
    );
  }

  const title =
    mode === "new"
      ? `Nuevo trámite — ${configQuery.data.code}`
      : `Continuar trámite — ${instanceQuery.data?.referenceNumber ?? instanceId}`;

  return (
    <div className="space-y-6">
      <PageHeaderCard
        title={title}
        subtitle="Complete cada paso del flujo. Las consultas externas no bloquean el avance salvo fallas obligatorias."
        actions={
          <Link
            to="/tramites"
            className="text-sm font-semibold text-flit-blue underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
          >
            Volver a la grilla
          </Link>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
        <WizardStepper
          steps={steps}
          currentIndex={currentStepIndex}
          stepIdPrefix={stepperId}
          onStepSelect={(index) => {
            if (index <= currentStepIndex) {
              setCurrentStepIndex(index);
            }
          }}
        />

        <section
          aria-labelledby={`${stepperId}-panel-heading`}
          className="rounded-flit-card bg-flit-card p-6 shadow-flit-card"
        >
          <h2
            id={`${stepperId}-panel-heading`}
            className="text-lg font-bold text-flit-blueDark"
          >
            {currentStep?.edge.name}
          </h2>

          <div className="mt-6 space-y-6">
            {currentStep?.isCopropiedad ? (
              <CopropiedadStep
                owners={owners}
                totalPercentage={ownershipTotal}
                ownershipValid={
                  ownershipQuery.data
                    ? ownershipQuery.data.ownershipValid
                    : Math.abs(ownershipTotal - 100) < 0.01
                      ? true
                      : ownershipTotal > 0
                        ? false
                        : null
                }
                validating={ownershipQuery.isFetching}
                onChange={(index, owner) =>
                  setOwners((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index ? owner : item,
                    ),
                  )
                }
                onAdd={() =>
                  setOwners((current) => [...current, createEmptyOwner()])
                }
                onRemove={(index) =>
                  setOwners((current) =>
                    current.filter((_, itemIndex) => itemIndex !== index),
                  )
                }
              />
            ) : (
              <DynamicFormFields
                sections={currentStep?.sections ?? []}
                values={fieldValues}
                errors={fieldErrors}
                onChange={handleFieldChange}
              />
            )}

            {instanceId && (
              <QueryBanners
                instanceId={instanceId}
                tenantId={tenantId!}
                polling={queryPolling}
              />
            )}
          </div>

          {submitError && (
            <p className="mt-4 text-sm text-flit-danger" role="alert">
              {submitError}
            </p>
          )}

          <div className="mt-8 flex flex-wrap gap-3">
            <GradientButton
              type="button"
              className="!h-11 !w-auto !px-6 !text-sm"
              disabled={currentStepIndex === 0}
              onClick={handleBack}
            >
              Anterior
            </GradientButton>
            <GradientButton
              type="button"
              variant="success"
              className="!h-11 !w-auto !px-6 !text-sm"
              disabled={
                createMutation.isPending ||
                saveOwnersMutation.isPending ||
                (currentStep?.isCopropiedad && !ownershipValid)
              }
              onClick={() => void handleNext()}
            >
              {currentStepIndex < steps.length - 1 ? "Siguiente" : "Finalizar"}
            </GradientButton>
          </div>
        </section>
      </div>
    </div>
  );
}
