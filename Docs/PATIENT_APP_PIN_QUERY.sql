-- PATIENT_APP_PIN lookup / maintenance queries
-- NOTE: Only SHA-256 hashes are stored (PIN_HASH). Plain PIN digits are never
-- saved in the database and cannot be recovered from PIN_HASH.

-- 1) List all active app-lock PIN records
SELECT
    PIN_ID,
    MR_NO,
    PIN_HASH,
    DEVICE_LABEL,
    TO_CHAR(CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') AS CREATED_AT,
    TO_CHAR(UPDATED_AT, 'YYYY-MM-DD HH24:MI:SS') AS UPDATED_AT,
    IS_ACTIVE
FROM PATIENT_APP_PIN
WHERE IS_ACTIVE = 'Y'
ORDER BY UPDATED_AT DESC;

-- 2) Look up by MR number
-- SELECT PIN_ID, MR_NO, PIN_HASH, DEVICE_LABEL, CREATED_AT, UPDATED_AT, IS_ACTIVE
-- FROM PATIENT_APP_PIN
-- WHERE MR_NO = '010-002-152'
-- ORDER BY UPDATED_AT DESC;

-- 3) Include inactive / cleared rows
-- SELECT *
-- FROM PATIENT_APP_PIN
-- ORDER BY UPDATED_AT DESC;

-- 4) Clear / deactivate all app PINs (patients must set a new PIN in the app)
-- UPDATE PATIENT_APP_PIN
--    SET IS_ACTIVE = 'N',
--        UPDATED_AT = SYSDATE
--  WHERE IS_ACTIVE = 'Y';
-- COMMIT;

-- 5) Clear / deactivate one patient
-- UPDATE PATIENT_APP_PIN
--    SET IS_ACTIVE = 'N',
--        UPDATED_AT = SYSDATE
--  WHERE MR_NO = '010-002-152'
--    AND IS_ACTIVE = 'Y';
-- COMMIT;

-- 6) Hard-delete all rows (optional)
-- DELETE FROM PATIENT_APP_PIN;
-- COMMIT;
