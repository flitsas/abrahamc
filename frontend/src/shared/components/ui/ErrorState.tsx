type ErrorStateProps = {
  error: Error | string;
  onRetry?: () => void;
  title?: string;
};

export function ErrorState({
  error,
  onRetry,
  title = "No se pudo cargar la información",
}: ErrorStateProps) {
  const message = typeof error === "string" ? error : error.message;

  return (
    <div
      className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-900"
      role="alert"
    >
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="mt-2 text-sm">{message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-4 rounded-lg bg-red-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-red-800"
        >
          Reintentar
        </button>
      )}
    </div>
  );
}
