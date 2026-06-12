import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  adminEdgeFormSchema,
  catalogEdgesResponseSchema,
  catalogFamiliesResponseSchema,
  createCatalogFamilyRequestSchema,
  createFormFieldRequestSchema,
  createFormSectionRequestSchema,
  createProcedureTypeRequestSchema,
  createProcedureTypeResponseSchema,
  procedureMatrixSchema,
  procedureTypesResponseSchema,
  ruleCatalogListSchema,
  simulateRulesRequestSchema,
  simulateRulesResponseSchema,
  type CreateFormFieldRequest,
  type CreateFormSectionRequest,
  type CreateProcedureTypeRequest,
  type SimulateRulesRequest,
} from "./parametrizacion.schemas.js";

type TenantParams = {
  tenantId: string;
};

type TypeCodeParams = TenantParams & {
  typeCode: string;
};

type EdgeFormParams = TypeCodeParams & {
  edgeCode: string;
};

export async function fetchCatalogFamilies() {
  const { data } = await apiClient.get(
    "/procedures-config/admin/catalog/families",
  );
  return catalogFamiliesResponseSchema.parse(data);
}

export async function createCatalogFamily(payload: {
  code: string;
  name: string;
  displayOrder?: number;
}) {
  const body = createCatalogFamilyRequestSchema.parse(payload);
  const { data } = await apiClient.post(
    "/procedures-config/admin/catalog/families",
    body,
  );
  return data;
}

export async function deleteCatalogFamily(code: string) {
  await apiClient.delete(
    `/procedures-config/admin/catalog/families/${encodeURIComponent(code)}`,
  );
}

export async function fetchCatalogEdges() {
  const { data } = await apiClient.get(
    "/procedures-config/admin/catalog/edges",
  );
  return catalogEdgesResponseSchema.parse(data);
}

export async function fetchProcedureTypes(params: TenantParams) {
  const { data } = await apiClient.get("/procedures-config/admin/types", {
    params: { tenantId: params.tenantId },
  });
  return procedureTypesResponseSchema.parse(data);
}

export async function fetchProcedureMatrix(params: TypeCodeParams) {
  const { data } = await apiClient.get(
    `/procedures-config/admin/types/${encodeURIComponent(params.typeCode)}/matrix`,
    { params: { tenantId: params.tenantId } },
  );
  return procedureMatrixSchema.parse(data);
}

export async function createProcedureType(payload: CreateProcedureTypeRequest) {
  const body = createProcedureTypeRequestSchema.parse(payload);
  const { data } = await apiClient.post("/procedures-config/admin/types", body);
  return createProcedureTypeResponseSchema.parse(data);
}

export async function patchProcedureEdge(
  params: TypeCodeParams & { edgeCode: string; isActive: boolean },
) {
  const { data } = await apiClient.patch(
    `/procedures-config/admin/types/${encodeURIComponent(params.typeCode)}/matrix/edges/${encodeURIComponent(params.edgeCode)}`,
    { isActive: params.isActive },
    { params: { tenantId: params.tenantId } },
  );
  return data;
}

export async function fetchEdgeForm(params: EdgeFormParams) {
  const { data } = await apiClient.get(
    `/procedures-config/admin/types/${encodeURIComponent(params.typeCode)}/edges/${encodeURIComponent(params.edgeCode)}/form`,
    { params: { tenantId: params.tenantId } },
  );
  return adminEdgeFormSchema.parse(data);
}

export async function createFormSection(
  params: EdgeFormParams,
  payload: CreateFormSectionRequest,
) {
  const body = createFormSectionRequestSchema.parse(payload);
  const { data } = await apiClient.post(
    `/procedures-config/admin/types/${encodeURIComponent(params.typeCode)}/edges/${encodeURIComponent(params.edgeCode)}/form/sections`,
    body,
    { params: { tenantId: params.tenantId } },
  );
  return data;
}

export async function createFormField(
  params: TypeCodeParams & { sectionId: string },
  payload: CreateFormFieldRequest,
) {
  const body = createFormFieldRequestSchema.parse(payload);
  const { data } = await apiClient.post(
    `/procedures-config/admin/types/${encodeURIComponent(params.typeCode)}/form/sections/${params.sectionId}/fields`,
    body,
    { params: { tenantId: params.tenantId } },
  );
  return data;
}

export async function fetchProcedureRules(
  tenantId: string,
  procedureTypeId: string,
) {
  const { data } = await apiClient.get("/procedures-config/rules", {
    params: { tenantId, procedureTypeId },
  });
  return ruleCatalogListSchema.parse(data);
}

export async function simulateProcedureRules(payload: SimulateRulesRequest) {
  const body = simulateRulesRequestSchema.parse(payload);
  const { data } = await apiClient.post("/procedures-config/rules/simulate", body);
  return simulateRulesResponseSchema.parse(data);
}

export function useCatalogFamilies(enabled = true) {
  return useQuery({
    queryKey: queryKeys.parametrizadorAdmin.families,
    queryFn: fetchCatalogFamilies,
    enabled,
    staleTime: 60_000,
  });
}

export function useCatalogEdges(enabled = true) {
  return useQuery({
    queryKey: queryKeys.parametrizadorAdmin.catalogEdges,
    queryFn: fetchCatalogEdges,
    enabled,
    staleTime: 60_000,
  });
}

export function useProcedureTypes(tenantId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: tenantId
      ? queryKeys.parametrizadorAdmin.types(tenantId)
      : ["parametrizador-admin", "types", "disabled"],
    queryFn: () => fetchProcedureTypes({ tenantId: tenantId! }),
    enabled: enabled && Boolean(tenantId),
    staleTime: 30_000,
  });
}

export function useProcedureMatrix(
  params: TypeCodeParams | null,
  enabled = true,
) {
  return useQuery({
    queryKey: params
      ? queryKeys.parametrizadorAdmin.matrix(params.tenantId, params.typeCode)
      : ["parametrizador-admin", "matrix", "disabled"],
    queryFn: () => fetchProcedureMatrix(params!),
    enabled: enabled && params !== null,
    staleTime: 15_000,
  });
}

export function useEdgeForm(params: EdgeFormParams | null, enabled = true) {
  return useQuery({
    queryKey: params
      ? queryKeys.parametrizadorAdmin.edgeForm(
          params.tenantId,
          params.typeCode,
          params.edgeCode,
        )
      : ["parametrizador-admin", "edge-form", "disabled"],
    queryFn: () => fetchEdgeForm(params!),
    enabled: enabled && params !== null,
    staleTime: 15_000,
  });
}

export function useProcedureRules(
  tenantId: string | undefined,
  procedureTypeId: string | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey:
      tenantId && procedureTypeId
        ? queryKeys.parametrizadorAdmin.rules(tenantId, procedureTypeId)
        : ["parametrizador-admin", "rules", "disabled"],
    queryFn: () => fetchProcedureRules(tenantId!, procedureTypeId!),
    enabled: enabled && Boolean(tenantId && procedureTypeId),
    staleTime: 30_000,
    retry: (failureCount, error) => {
      if (axios.isAxiosError(error) && error.response?.status === 403) {
        return false;
      }
      return failureCount < 2;
    },
  });
}

export function useCreateCatalogFamily() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createCatalogFamily,
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.families,
      });
    },
  });
}

export function useDeleteCatalogFamily() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: deleteCatalogFamily,
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.families,
      });
    },
  });
}

export function useCreateProcedureType() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createProcedureType,
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.types(variables.tenantId),
      });
    },
  });
}

export function usePatchProcedureEdge() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: patchProcedureEdge,
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.matrix(
          variables.tenantId,
          variables.typeCode,
        ),
      });
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.types(variables.tenantId),
      });
    },
  });
}

export function useCreateFormSection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      params,
      payload,
    }: {
      params: EdgeFormParams;
      payload: CreateFormSectionRequest;
    }) => createFormSection(params, payload),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.edgeForm(
          variables.params.tenantId,
          variables.params.typeCode,
          variables.params.edgeCode,
        ),
      });
    },
  });
}

export function useCreateFormField() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      params,
      payload,
    }: {
      params: TypeCodeParams & { sectionId: string; edgeCode: string };
      payload: CreateFormFieldRequest;
    }) => createFormField(params, payload),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.parametrizadorAdmin.edgeForm(
          variables.params.tenantId,
          variables.params.typeCode,
          variables.params.edgeCode,
        ),
      });
    },
  });
}

export function useSimulateRules() {
  return useMutation({
    mutationFn: simulateProcedureRules,
  });
}
