export const DOCUMENTS_EDGE_CODE = "documentos";
export const MIN_ACTIVE_DATA_EDGES = 4;

export type PipelineEdgeDraft = {
  edgeCode: string;
  isActive: boolean;
};

export function countActiveDataEdges(edges: PipelineEdgeDraft[]): number {
  return edges.filter(
    (edge) =>
      edge.isActive &&
      edge.edgeCode.toLowerCase() !== DOCUMENTS_EDGE_CODE.toLowerCase(),
  ).length;
}

export function validateMinActiveDataEdges(edges: PipelineEdgeDraft[]): {
  isValid: boolean;
  message: string | null;
} {
  const count = countActiveDataEdges(edges);
  if (count < MIN_ACTIVE_DATA_EDGES) {
    return {
      isValid: false,
      message: `El pipeline debe incluir al menos ${MIN_ACTIVE_DATA_EDGES} pasos activos de datos (excluyendo documentos). Actual: ${count}.`,
    };
  }
  return { isValid: true, message: null };
}
