import type { InputHTMLAttributes, ReactNode } from "react";

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string;
  icon?: ReactNode;
};

export function TextField({
  label,
  icon,
  id,
  className = "",
  ...props
}: TextFieldProps) {
  const fieldId = id ?? props.name;

  return (
    <div>
      <label
        htmlFor={fieldId}
        className="block text-sm font-semibold text-flit-blueDark"
      >
        {label}
      </label>
      <div className="relative mt-2">
        {icon && (
          <span
            className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-flit-blue"
            aria-hidden="true"
          >
            {icon}
          </span>
        )}
        <input
          id={fieldId}
          className={`h-12 w-full rounded-[10px] border border-flit-border bg-white text-sm text-flit-blueDark placeholder:text-flit-muted focus:border-flit-blue focus:outline-none focus:ring-2 focus:ring-flit-blue/20 ${icon ? "pl-11 pr-4" : "px-4"} ${className}`}
          {...props}
        />
      </div>
    </div>
  );
}
