-- Verify PATIENT_RECENT_ACTIVITY exists and has expected columns.

-- STEP 1: Table present?
SELECT table_name
FROM user_tables
WHERE table_name = 'PATIENT_RECENT_ACTIVITY';

-- STEP 2: Columns
SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'PATIENT_RECENT_ACTIVITY'
ORDER BY column_id;

-- STEP 3: Sequence + unique index
SELECT sequence_name FROM user_sequences
WHERE sequence_name = 'PATIENT_RECENT_ACTIVITY_SEQ';

SELECT index_name, uniqueness
FROM user_indexes
WHERE table_name = 'PATIENT_RECENT_ACTIVITY';

-- STEP 4: Row count
SELECT COUNT(*) AS total_rows FROM PATIENT_RECENT_ACTIVITY;
