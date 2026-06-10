import { useMutation } from "@tanstack/react-query";
import { apiClient } from "../../../shared/api/client.js";
import {
  passwordResetConfirmResponseSchema,
  passwordResetConfirmSchema,
  passwordResetRequestResponseSchema,
  passwordResetRequestSchema,
  type PasswordResetConfirm,
  type PasswordResetRequest,
} from "./password-reset.schemas.js";

export async function requestPasswordReset(request: PasswordResetRequest) {
  const payload = passwordResetRequestSchema.parse(request);
  const { data } = await apiClient.post(
    "/auth/password-reset/request",
    payload,
  );
  return passwordResetRequestResponseSchema.parse(data);
}

export async function confirmPasswordReset(request: PasswordResetConfirm) {
  const payload = passwordResetConfirmSchema.parse(request);
  const { data } = await apiClient.post(
    "/auth/password-reset/confirm",
    payload,
  );
  return passwordResetConfirmResponseSchema.parse(data);
}

export function useRequestPasswordReset() {
  return useMutation({ mutationFn: requestPasswordReset });
}

export function useConfirmPasswordReset() {
  return useMutation({ mutationFn: confirmPasswordReset });
}
