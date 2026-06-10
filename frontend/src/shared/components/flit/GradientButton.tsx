import type { ButtonHTMLAttributes, ReactNode } from "react";

type GradientButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
  variant?: "primary" | "success";
};

export function GradientButton({
  children,
  variant = "primary",
  className = "",
  type = "button",
  ...props
}: GradientButtonProps) {
  const gradient =
    variant === "success" ? "bg-flit-success" : "bg-flit-primary";

  return (
    <button
      type={type}
      className={`inline-flex h-14 w-full items-center justify-center rounded-flit-pill ${gradient} px-8 text-base font-semibold text-white shadow-flit-button transition hover:brightness-105 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue disabled:cursor-not-allowed disabled:opacity-60 ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}
