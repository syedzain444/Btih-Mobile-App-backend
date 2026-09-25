-- =============================================================================
-- POST /api/Auth/verify-login-otp — what lives in the DB?
-- =============================================================================
-- IMPORTANT:
--   The login OTP itself is NOT stored in Oracle.
--   It lives only in the API server memory cache (key: login_challenge:{id})
--   for a few minutes after POST /api/Auth/login returns requiresOtp=true.
--
--   When verify-login-otp succeeds WITH trustDevice=true, a row is written to:
--     PATIENT_TRUSTED_DEVICE
--
-- Run as the same Oracle user as HMISConnection in appsettings
-- (same schema as PATIENT_DEVICE_TOKEN / PATIENT_NOTIFICATION).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- STEP 1: Confirm schema
-- -----------------------------------------------------------------------------
SELECT USER AS connected_schema FROM DUAL;

-- -----------------------------------------------------------------------------
-- STEP 2: Confirm the table exists
-- -----------------------------------------------------------------------------
SELECT table_name
FROM user_tables
WHERE table_name = 'PATIENT_TRUSTED_DEVICE';

-- If 0 rows → run Docs/PATIENT_TRUSTED_DEVICE.sql first, then re-check:
SELECT owner, table_name
FROM all_tables
WHERE table_name = 'PATIENT_TRUSTED_DEVICE'
ORDER BY owner, table_name;

-- -----------------------------------------------------------------------------
-- STEP 3: Table structure
-- -----------------------------------------------------------------------------
SELECT column_name, data_type, data_length, nullable
FROM user_tab_columns
WHERE table_name = 'PATIENT_TRUSTED_DEVICE'
ORDER BY column_id;

-- -----------------------------------------------------------------------------
-- STEP 4: Latest trusted devices (after successful verify-login-otp + trust)
-- Oracle 11g-safe
-- -----------------------------------------------------------------------------
SELECT *
FROM (
  SELECT trusted_device_id,
         mr_no,
         device_install_id,
         device_label,
         platform,
         trusted_at,
         last_login_at,
         expires_at,
         is_active,
         CASE
           WHEN is_active = 'Y' AND expires_at >= SYSDATE THEN 'ACTIVE'
           WHEN is_active = 'N' THEN 'REVOKED'
           ELSE 'EXPIRED'
         END AS trust_status
  FROM patient_trusted_device
  ORDER BY NVL(last_login_at, trusted_at) DESC
)
WHERE ROWNUM <= 50;

-- -----------------------------------------------------------------------------
-- STEP 5: Devices for one patient (change MR number)
-- -----------------------------------------------------------------------------
SELECT trusted_device_id,
       mr_no,
       device_install_id,
       device_label,
       platform,
       trusted_at,
       last_login_at,
       expires_at,
       is_active
FROM patient_trusted_device
WHERE mr_no = '010-002-152'   -- << change MR No
ORDER BY NVL(last_login_at, trusted_at) DESC;

-- -----------------------------------------------------------------------------
-- STEP 6: Active (not revoked / not expired) devices only
-- -----------------------------------------------------------------------------
SELECT mr_no,
       device_label,
       platform,
       trusted_at,
       last_login_at,
       expires_at
FROM patient_trusted_device
WHERE is_active = 'Y'
  AND expires_at >= SYSDATE
ORDER BY last_login_at DESC NULLS LAST;

-- -----------------------------------------------------------------------------
-- How to see the OTP itself (NOT in DB)
-- -----------------------------------------------------------------------------
-- 1) Call POST /api/Auth/login with a new / untrusted device.
-- 2) Response includes loginChallengeId (and sometimes debugOtp if SMS fails
--    and ReturnDebugOtpOnFailure / Development is enabled).
-- 3) Call POST /api/Auth/verify-login-otp with:
--      { "loginChallengeId": "...", "otp": "######", "deviceInstallId": "...",
--        "trustDevice": true }
-- 4) Then re-run STEP 4 / STEP 5 — a new PATIENT_TRUSTED_DEVICE row appears.
--
-- There is no PATIENT_LOGIN_OTP (or similar) table for this endpoint.
