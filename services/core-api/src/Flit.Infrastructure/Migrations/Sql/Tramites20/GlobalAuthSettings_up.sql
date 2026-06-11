-- =====================================================================================
-- FLIT Trámites 2.0 — Configuración global de autenticación (singleton, #9549)
-- Un solo registro para toda la plataforma; editable por BD sin redeploy.
-- =====================================================================================

CREATE TABLE IF NOT EXISTS identity.global_auth_settings (
  id                          uuid        PRIMARY KEY
                                          DEFAULT '00000000-0000-7000-8001-000000000001'::uuid,
  invitation_ttl_minutes      integer     NOT NULL DEFAULT 60
                                          CHECK (invitation_ttl_minutes > 0 AND invitation_ttl_minutes <= 10080),
  password_reset_ttl_minutes  integer     NOT NULL DEFAULT 30
                                          CHECK (password_reset_ttl_minutes > 0 AND password_reset_ttl_minutes <= 1440),
  access_token_ttl_minutes    integer     NOT NULL DEFAULT 15
                                          CHECK (access_token_ttl_minutes > 0 AND access_token_ttl_minutes <= 1440),
  refresh_token_ttl_days      integer     NOT NULL DEFAULT 7
                                          CHECK (refresh_token_ttl_days > 0 AND refresh_token_ttl_days <= 90),
  created_at                  timestamptz NOT NULL DEFAULT now(),
  updated_at                  timestamptz NOT NULL DEFAULT now(),
  updated_by                  uuid        NOT NULL DEFAULT '00000000-0000-7000-8000-000000000001'::uuid,
  row_version                 integer     NOT NULL DEFAULT 1,
  CONSTRAINT ck_global_auth_settings_singleton
    CHECK (id = '00000000-0000-7000-8001-000000000001'::uuid)
);

COMMENT ON TABLE identity.global_auth_settings IS
  '@context:identity Configuración global singleton de autenticación (toda la plataforma).';
COMMENT ON COLUMN identity.global_auth_settings.invitation_ttl_minutes IS
  'Vigencia del enlace de activación de cuenta nueva (onboarding_invitations).';
COMMENT ON COLUMN identity.global_auth_settings.password_reset_ttl_minutes IS
  'Vigencia del enlace «olvidé mi contraseña» (password_reset_tokens).';
COMMENT ON COLUMN identity.global_auth_settings.access_token_ttl_minutes IS
  'Vigencia del JWT de acceso (sesión activa). Reservado; hoy también en appsettings Jwt:AccessTokenMinutes.';
COMMENT ON COLUMN identity.global_auth_settings.refresh_token_ttl_days IS
  'Vigencia del refresh token (renovar sesión). Reservado; hoy también en appsettings Jwt:RefreshTokenDays.';
COMMENT ON COLUMN identity.global_auth_settings.row_version IS
  'Contador interno de ediciones (concurrencia optimista). No define vencimiento de tokens.';

CREATE TRIGGER tr_global_auth_settings_before_update_row_version
  BEFORE UPDATE ON identity.global_auth_settings
  FOR EACH ROW EXECUTE FUNCTION audit.increment_row_version();

INSERT INTO identity.global_auth_settings (
  id,
  invitation_ttl_minutes,
  password_reset_ttl_minutes,
  access_token_ttl_minutes,
  refresh_token_ttl_days,
  updated_by
) VALUES (
  '00000000-0000-7000-8001-000000000001',
  60,
  30,
  15,
  7,
  '00000000-0000-7000-8000-000000000001'
)
ON CONFLICT (id) DO NOTHING;

-- Compatibilidad si ya se aplicó el nombre anterior de la migración
DROP TABLE IF EXISTS identity.auth_token_settings CASCADE;
