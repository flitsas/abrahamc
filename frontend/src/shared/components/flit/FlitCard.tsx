import type { ReactNode } from "react";

type FlitCardProps = {
  children: ReactNode;
  className?: string;
  accent?: boolean;
};

export function FlitCard({
  children,
  className = "",
  accent = false,
}: FlitCardProps) {
  return (
    <article
      className={`rounded-flit-card bg-flit-card p-6 shadow-flit-card ${
        accent ? "flit-card-accent" : ""
      } ${className}`}
    >
      {children}
    </article>
  );
}
