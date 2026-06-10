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
      className="rounded-flit-card border border-flit-danger/25 bg-flit-danger/10 p-6 text-flit-danger"
      role="alert"
    >
      <h2 className="text-lg font-semibold text-flit-blueDark">{title}</h2>
      <p className="mt-2 text-sm">{message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-4 rounded-flit-pill bg-flit-danger px-5 py-2 text-sm font-semibold text-white transition hover:brightness-105 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-flit-blue"
        >
          Reintentar
        </button>
      )}
    </div>
  );
}
