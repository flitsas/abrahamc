type PermissionsStaleListener = () => void;

const listeners = new Set<PermissionsStaleListener>();

export function subscribePermissionsStale(
  listener: PermissionsStaleListener,
): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function notifyPermissionsStale(): void {
  listeners.forEach((listener) => listener());
}
