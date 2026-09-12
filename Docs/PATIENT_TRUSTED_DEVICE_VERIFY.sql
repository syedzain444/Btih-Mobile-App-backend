-- =============================================================================
-- STEP 1: Confirm which Oracle user you are connected as
-- Must match HMISConnection in appsettings (usually user HMIS)
-- =============================================================================
SELECT USER AS connected_schema FROM DUAL;

-- =============================================================================
-- STEP 2: Check if trusted-device table exists in YOUR schema
-- =============================================================================
SELECT table_name
FROM user_tables
WHERE table_name = 'PATIENT_TRUSTED_DEVICE';

-- If STEP 2 returns 0 rows, run PATIENT_TRUSTED_DEVICE.sql first.
-- If missing but you see it under another owner, you may be on the wrong user:
SELECT owner, table_name
FROM all_tables
WHERE table_name = 'PATIENT_TRUSTED_DEVICE'
ORDER BY owner, table_name;

-- =============================================================================
-- STEP 3: Verify sequence and trigger
-- =============================================================================
SELECT sequence_name, last_number
FROM user_sequences
WHERE sequence_name = 'PATIENT_TRUSTED_DEVICE_SEQ';

SELECT trigger_name, status
FROM user_triggers
WHERE table_name = 'PATIENT_TRUSTED_DEVICE';

-- =============================================================================
-- STEP 4: Verify table structure
-- =============================================================================
SELECT column_name, data_type, data_length, nullable
FROM user_tab_columns
WHERE table_name = 'PATIENT_TRUSTED_DEVICE'
ORDER BY column_id;

SELECT index_name, uniqueness
FROM user_indexes
WHERE table_name = 'PATIENT_TRUSTED_DEVICE'
ORDER BY index_name;

-- =============================================================================
-- STEP 5: Count rows (0 is OK until a patient trusts a device)
-- =============================================================================
SELECT COUNT(*) AS total_rows FROM PATIENT_TRUSTED_DEVICE;

-- =============================================================================
-- STEP 6: Trusted devices for a patient (change MR number)
-- =============================================================================
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
WHERE mr_no = '010-002-152'
ORDER BY NVL(last_login_at, trusted_at) DESC;
