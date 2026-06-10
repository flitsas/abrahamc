import type { ReactNode } from "react";
import { useEffect, useState } from "react";

const LOGO_LEFT_SRC = "/flitsas_logo_izq.jpg";
const LOGO_RIGHT_SRC = "/flitsas_logo_der.jpg";
export const SPLIT_DURATION_MS = 900;

type SplitCurtainVariant = "logo" | "solid" | "content";

type SplitCurtainProps = {
  open: boolean;
  onComplete: () => void;
  variant?: SplitCurtainVariant;
  children?: ReactNode;
  onActivate?: () => void;
  activateLabel?: string;
};

export function SplitCurtain({
  open,
  onComplete,
  variant = "solid",
  children,
  onActivate,
  activateLabel = "Login",
}: SplitCurtainProps) {
  const [splitting, setSplitting] = useState(open);
  const prefersReducedMotion =
    typeof window !== "undefined" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  useEffect(() => {
    if (open) {
      setSplitting(true);
    }
  }, [open]);

  useEffect(() => {
    if (!splitting) {
      return;
    }

    const timer = window.setTimeout(
      onComplete,
      prefersReducedMotion ? 0 : SPLIT_DURATION_MS,
    );

    return () => window.clearTimeout(timer);
  }, [splitting, onComplete, prefersReducedMotion]);

  function handleActivate() {
    if (splitting || !onActivate) {
      return;
    }

    if (prefersReducedMotion) {
      onActivate();
      return;
    }

    setSplitting(true);
  }

  if (variant === "content" && children) {
    return (
      <div
        className={`fixed inset-0 z-50 font-flit ${splitting ? "pointer-events-none" : ""}`}
        aria-hidden={splitting}
      >
        <div
          className={`absolute inset-y-0 left-0 w-1/2 overflow-hidden transition-transform duration-[900ms] ease-in-out ${
            splitting ? "-translate-x-full" : "translate-x-0"
          }`}
        >
          <div className="h-full w-[200vw]">{children}</div>
        </div>

        <div
          className={`absolute inset-y-0 right-0 w-1/2 overflow-hidden transition-transform duration-[900ms] ease-in-out ${
            splitting ? "translate-x-full" : "translate-x-0"
          }`}
        >
          <div className="h-full w-[200vw] -translate-x-[100vw]">
            {children}
          </div>
        </div>
      </div>
    );
  }

  const panelClass =
    variant === "logo" ? "flex items-center bg-flit-bg" : "bg-flit-bg";

  return (
    <div
      className={`fixed inset-0 z-50 font-flit ${splitting ? "pointer-events-none" : ""}`}
      aria-hidden={splitting}
    >
      <div
        className={`absolute inset-y-0 left-0 w-1/2 transition-transform duration-[900ms] ease-in-out ${panelClass} ${
          variant === "logo" ? "justify-end" : ""
        } ${splitting ? "-translate-x-full" : "translate-x-0"}`}
      >
        {variant === "logo" && (
          <img
            src={LOGO_LEFT_SRC}
            alt=""
            className="h-40 max-w-none object-contain sm:h-48"
            draggable={false}
          />
        )}
      </div>

      <div
        className={`absolute inset-y-0 right-0 w-1/2 transition-transform duration-[900ms] ease-in-out ${panelClass} ${
          variant === "logo" ? "justify-start" : ""
        } ${splitting ? "translate-x-full" : "translate-x-0"}`}
      >
        {variant === "logo" && (
          <img
            src={LOGO_RIGHT_SRC}
            alt=""
            className="h-40 max-w-none object-contain sm:h-48"
            draggable={false}
          />
        )}
      </div>

      {onActivate && (
        <button
          type="button"
          onClick={handleActivate}
          disabled={splitting}
          aria-label="Ir a iniciar sesión"
          className={`group absolute inset-0 flex flex-col items-center justify-center gap-8 border-0 bg-transparent transition-opacity duration-300 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-flit-blue disabled:cursor-default ${
            splitting ? "opacity-0" : "opacity-100"
          }`}
        >
          <div className="h-40 sm:h-48" aria-hidden="true" />
          <span className="flit-welcome-login-glow text-xl font-bold uppercase tracking-[0.4em] transition-transform duration-300 group-hover:scale-105 sm:text-2xl">
            {activateLabel}
          </span>
        </button>
      )}
    </div>
  );
}
