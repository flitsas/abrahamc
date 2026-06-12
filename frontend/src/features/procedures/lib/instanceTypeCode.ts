import type { ProcedureInstanceDetail } from "../api/procedure-instances.schemas.js";

export function resolveProcedureTypeCode(
  instance: ProcedureInstanceDetail,
): string | undefined {
  if (instance.procedureTypeCode) {
    return instance.procedureTypeCode;
  }

  try {
    const snapshot = JSON.parse(instance.configSnapshot) as {
      code?: string;
      procedureTypeCode?: string;
    };
    return snapshot.code ?? snapshot.procedureTypeCode;
  } catch {
    return undefined;
  }
}
