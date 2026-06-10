-- HU #9418 — lookup de invitación por token_hash sin contexto de tenant (SECURITY DEFINER).
CREATE OR REPLACE FUNCTION identity.find_onboarding_by_token_hash(p_token_hash text)
RETURNS TABLE (
  id uuid,
  tenant_id uuid,
  email text,
  invited_role_id uuid,
  token_hash text,
  signature text,
  status text,
  expires_at timestamptz,
  consumed_at timestamptz
)
LANGUAGE sql
SECURITY DEFINER
STABLE
SET search_path = identity, pg_temp
AS $$
  SELECT
    i.id,
    i.tenant_id,
    i.email::text,
    i.invited_role_id,
    i.token_hash,
    i.signature,
    i.status,
    i.expires_at,
    i.consumed_at
  FROM identity.onboarding_invitations i
  WHERE i.token_hash = p_token_hash
    AND i.deleted_at IS NULL;
$$;

COMMENT ON FUNCTION identity.find_onboarding_by_token_hash(text) IS
  'Resuelve invitación de onboarding para preview/activate antes de fijar GUC de tenant.';

CREATE OR REPLACE FUNCTION identity.email_has_active_user(p_email citext)
RETURNS boolean
LANGUAGE sql
SECURITY DEFINER
STABLE
SET search_path = identity, pg_temp
AS $$
  SELECT EXISTS (
    SELECT 1
    FROM identity.users u
    WHERE u.email = p_email
      AND u.account_state = 'active'
      AND u.deleted_at IS NULL
  );
$$;
