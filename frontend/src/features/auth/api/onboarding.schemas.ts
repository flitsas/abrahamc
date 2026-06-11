import { z } from "zod";

export const passwordPolicySchema = z.object({
  minLength: z.number(),
  requireUppercase: z.boolean(),
  requireLowercase: z.boolean(),
  requireDigit: z.boolean(),
  requireSymbol: z.boolean(),
});

export const invitationPreviewSchema = z.object({
  valid: z.literal(true),
  email: z.string().email(),
  tenantId: z.string().uuid(),
  expiresAt: z.string(),
  passwordPolicy: passwordPolicySchema,
});

export const activateAccountRequestSchema = z.object({
  invitationId: z.string().uuid(),
  token: z.string().min(1),
  signature: z.string().min(1),
  password: z.string().min(1),
});

export const activateAccountResponseSchema = z.object({
  userId: z.string().uuid(),
  email: z.string().email(),
  message: z.string(),
});

export const onboardingTokenSettingsSchema = z.object({
  invitationTtlMinutes: z.number().int().positive(),
  passwordResetTtlMinutes: z.number().int().positive(),
  accessTokenTtlMinutes: z.number().int().positive(),
  refreshTokenTtlDays: z.number().int().positive(),
});

export type PasswordPolicy = z.infer<typeof passwordPolicySchema>;
export type OnboardingTokenSettings = z.infer<
  typeof onboardingTokenSettingsSchema
>;
export type InvitationPreview = z.infer<typeof invitationPreviewSchema>;
export type ActivateAccountRequest = z.infer<
  typeof activateAccountRequestSchema
>;
