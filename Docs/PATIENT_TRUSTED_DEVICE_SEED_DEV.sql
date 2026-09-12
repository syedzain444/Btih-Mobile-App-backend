-- Optional dev seed for contact 03339993577 when SMS is unavailable.
-- Prefer API dev auto-trust (Auth:DevAutoTrustContacts in appsettings.Development.json)
-- which trusts the device automatically on the next password login.
--
-- Use this script only if you need to pre-seed before the first login.
-- Replace :MR_NO with the patient's MR from HMIS (query below).

-- Find MR for the test contact:
-- SELECT mr_no, contact_no FROM patient_information WHERE contact_no LIKE '%339993577%';

-- Fixed dev credentials (must match if you hard-code them in a test client):
--   deviceInstallId = dev-trusted-03339993577
--   deviceTrustToken = DEV-TRUST-03339993577
--
-- TRUST_TOKEN_HASH = SHA-256 hex of:
--   {JwtSettings:SecretKey}:DEV-TRUST-03339993577
-- With Development secret from appsettings.Development.json that hash is:
--   cc7f89f79e4cf1e3a3216198a1c5f05a19bd6214e8af11d57fc8129d590cde28
-- (Recompute if your SecretKey differs — or let the API auto-trust on login instead.)

/*
MERGE INTO PATIENT_TRUSTED_DEVICE t
USING (
    SELECT
        '010-002-152' AS MR_NO,
        'dev-trusted-03339993577' AS DEVICE_INSTALL_ID
    FROM dual
) s
ON (t.MR_NO = s.MR_NO AND t.DEVICE_INSTALL_ID = s.DEVICE_INSTALL_ID)
WHEN MATCHED THEN
    UPDATE SET
        TRUST_TOKEN_HASH = :trust_token_hash,
        DEVICE_LABEL = 'Dev trusted phone',
        PLATFORM = 'android',
        TRUSTED_AT = SYSDATE,
        LAST_LOGIN_AT = SYSDATE,
        EXPIRES_AT = SYSDATE + 90,
        IS_ACTIVE = 'Y'
WHEN NOT MATCHED THEN
    INSERT (
        TRUSTED_DEVICE_ID,
        MR_NO,
        DEVICE_INSTALL_ID,
        TRUST_TOKEN_HASH,
        DEVICE_LABEL,
        PLATFORM,
        TRUSTED_AT,
        LAST_LOGIN_AT,
        EXPIRES_AT,
        IS_ACTIVE
    ) VALUES (
        PATIENT_TRUSTED_DEVICE_SEQ.NEXTVAL,
        s.MR_NO,
        s.DEVICE_INSTALL_ID,
        :trust_token_hash,
        'Dev trusted phone',
        'android',
        SYSDATE,
        SYSDATE,
        SYSDATE + 90,
        'Y'
    );
*/
