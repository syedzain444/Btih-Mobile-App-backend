-- =============================================================================
-- STEP 1: Confirm which Oracle user you are connected as
-- Must match HMISConnection in appsettings (usually user HMIS)
-- =============================================================================
SELECT USER AS connected_schema FROM DUAL;

-- =============================================================================
-- STEP 2: Check if notification + device-token tables exist in YOUR schema
-- =============================================================================
SELECT table_name
FROM user_tables
WHERE table_name IN ('PATIENT_NOTIFICATION', 'PATIENT_DEVICE_TOKEN')
ORDER BY table_name;

-- If STEP 2 returns 0 rows for PATIENT_NOTIFICATION but PATIENT_DEVICE_TOKEN exists,
-- you are on the correct user — run PATIENT_NOTIFICATION.sql next.

-- If BOTH are missing, you may be connected as the wrong user.
-- Search all schemas you can see:
SELECT owner, table_name
FROM all_tables
WHERE table_name IN ('PATIENT_NOTIFICATION', 'PATIENT_DEVICE_TOKEN')
ORDER BY owner, table_name;

-- =============================================================================
-- STEP 3: After running PATIENT_NOTIFICATION.sql, verify structure
-- =============================================================================
SELECT column_name, data_type, data_length, nullable
FROM user_tab_columns
WHERE table_name = 'PATIENT_NOTIFICATION'
ORDER BY column_id;

-- =============================================================================
-- STEP 4: Count rows (0 is OK until a push is sent)
-- =============================================================================
SELECT COUNT(*) AS total_rows FROM PATIENT_NOTIFICATION;

-- =============================================================================
-- STEP 5: Latest notifications for a patient (change MR number)
-- =============================================================================
SELECT notification_id, mr_no, notification_type, title, is_read, created_at
FROM patient_notification
WHERE mr_no = '010-002-152'
ORDER BY created_at DESC;
