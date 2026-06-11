/** Texto legible en español para minutos de vigencia (p. ej. TTL de invitación). */
export function formatDurationMinutes(minutes: number): string {
  if (minutes < 60) {
    return `${minutes} minuto${minutes === 1 ? "" : "s"}`;
  }

  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;

  if (remainder === 0) {
    return `${hours} hora${hours === 1 ? "" : "s"}`;
  }

  return `${hours} h ${remainder} min`;
}
