import type { WizardStep } from "../lib/wizardSteps.js";

type WizardStepperProps = {
  steps: WizardStep[];
  currentIndex: number;
  stepIdPrefix: string;
  onStepSelect?: (index: number) => void;
};

export function WizardStepper({
  steps,
  currentIndex,
  stepIdPrefix,
  onStepSelect,
}: WizardStepperProps) {
  return (
    <nav aria-label="Pasos del trámite">
      <ol className="space-y-2">
        {steps.map((step, index) => {
          const isCurrent = index === currentIndex;
          const isComplete = index < currentIndex;
          const stepId = `${stepIdPrefix}-step-${index}`;
          const canNavigate = onStepSelect && index <= currentIndex;

          return (
            <li key={step.edge.code}>
              {canNavigate ? (
                <button
                  type="button"
                  id={stepId}
                  onClick={() => onStepSelect(index)}
                  aria-current={isCurrent ? "step" : undefined}
                  className={`flex w-full items-start gap-3 rounded-[10px] border px-4 py-3 text-left transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue ${
                    isCurrent
                      ? "border-flit-blue bg-flit-blue/5"
                      : isComplete
                        ? "border-flit-green/30 bg-flit-green/5"
                        : "border-flit-border bg-white"
                  }`}
                >
                  <StepIndicator
                    index={index}
                    isCurrent={isCurrent}
                    isComplete={isComplete}
                  />
                  <StepLabel step={step} isCurrent={isCurrent} />
                </button>
              ) : (
                <div
                  id={stepId}
                  aria-current={isCurrent ? "step" : undefined}
                  className={`flex items-start gap-3 rounded-[10px] border px-4 py-3 ${
                    isCurrent
                      ? "border-flit-blue bg-flit-blue/5"
                      : "border-flit-border bg-white opacity-70"
                  }`}
                >
                  <StepIndicator
                    index={index}
                    isCurrent={isCurrent}
                    isComplete={isComplete}
                  />
                  <StepLabel step={step} isCurrent={isCurrent} />
                </div>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

function StepIndicator({
  index,
  isCurrent,
  isComplete,
}: {
  index: number;
  isCurrent: boolean;
  isComplete: boolean;
}) {
  return (
    <span
      className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-bold ${
        isCurrent
          ? "bg-flit-blue text-white"
          : isComplete
            ? "bg-flit-green text-white"
            : "bg-flit-draft/20 text-flit-muted"
      }`}
      aria-hidden="true"
    >
      {index + 1}
    </span>
  );
}

function StepLabel({
  step,
  isCurrent,
}: {
  step: WizardStep;
  isCurrent: boolean;
}) {
  return (
    <span>
      <span
        className={`block text-sm font-semibold ${
          isCurrent ? "text-flit-blueDark" : "text-flit-blueText"
        }`}
      >
        {step.edge.name}
      </span>
      {step.edge.roleLabel && (
        <span className="mt-0.5 block text-xs text-flit-muted">
          {step.edge.roleLabel}
        </span>
      )}
    </span>
  );
}
