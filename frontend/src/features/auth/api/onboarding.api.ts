import { useMutation, useQuery } from "@tanstack/react-query";
import { apiClient } from "../../../shared/api/client.js";
import { queryKeys } from "../../../shared/lib/queryKeys.js";
import {
  activateAccountRequestSchema,
  activateAccountResponseSchema,
  invitationPreviewSchema,
  type ActivateAccountRequest,
} from "./onboarding.schemas.js";

export type InvitationQueryParams = {
  invitationId: string;
  token: string;
  signature: string;
};

export async function previewInvitation(params: InvitationQueryParams) {
  const { data } = await apiClient.get("/identity/onboarding/preview", {
    params,
  });
  return invitationPreviewSchema.parse(data);
}

export async function activateAccount(request: ActivateAccountRequest) {
  const payload = activateAccountRequestSchema.parse(request);
  const { data } = await apiClient.post(
    "/identity/onboarding/activate",
    payload,
  );
  return activateAccountResponseSchema.parse(data);
}

export function useInvitationPreview(
  params: InvitationQueryParams | null,
  enabled = true,
) {
  return useQuery({
    queryKey: queryKeys.auth.invitationPreview(params),
    queryFn: () => previewInvitation(params!),
    enabled:
      enabled &&
      Boolean(params?.invitationId && params?.token && params?.signature),
    retry: false,
    staleTime: 60_000,
  });
}

export function useActivateAccount() {
  return useMutation({ mutationFn: activateAccount });
}
