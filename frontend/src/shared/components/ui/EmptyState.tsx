type EmptyStateProps = {
  title?: string;
  description?: string;
};

export function EmptyState({
  title = "Sin resultados",
  description = "No hay datos para mostrar en este momento.",
}: EmptyStateProps) {
  return (
    <div className="rounded-xl border border-dashed border-slate-300 bg-white p-8 text-center">
      <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
      <p className="mt-2 text-sm text-slate-600">{description}</p>
    </div>
  );
}
