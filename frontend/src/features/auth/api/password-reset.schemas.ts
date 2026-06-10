import { z } from "zod";

export const passwordResetRequestSchema = z.object({
  email: z.string().email(),
});

export const passwordResetRequestResponseSchema = z.object({
  message: z.string(),
});

export const passwordResetConfirmSchema = z.object({
  resetToken: z.string().min(1),
  newPassword: z.string().min(1),
});

export const passwordResetConfirmResponseSchema = z.object({
  message: z.string(),
});

export type PasswordResetRequest = z.infer<typeof passwordResetRequestSchema>;
export type PasswordResetConfirm = z.infer<typeof passwordResetConfirmSchema>;
