-- =============================================================================
-- Patient lookup: 010-027-090
-- Finds this MR No in core HMIS + mobile-portal tables, and discovers every
-- table in the schema that has a row for this patient.
--
-- Run as the same Oracle user as HMISConnection (appsettings).
-- =============================================================================

DEFINE mr_no = '010-027-090'

SELECT USER AS connected_schema, '&mr_no' AS looking_for FROM DUAL;

-- =============================================================================
-- A) Discover ALL tables in this schema that have an MR_NO column
-- =============================================================================
SELECT table_name, column_name, data_type, data_length
FROM user_tab_columns
WHERE column_name IN ('MR_NO', 'MRNO', 'PATIENT_MR_NO')
ORDER BY table_name, column_name;

-- =============================================================================
-- B) Core HMIS patient record
-- =============================================================================
SELECT pm.MR_NO,
       pm.FIRST_NAME,
       pm.LAST_NAME,
       pm.PATIENT_PASSWORD AS has_password_set  -- do not display raw password in reports
FROM PATIENT_MST pm
WHERE pm.MR_NO = '&mr_no';

SELECT pi.MR_NO,
       pi.CONTACT_NO,
       pi.*
FROM PATIENT_INFORMATION pi
WHERE pi.MR_NO = '&mr_no';

-- Safer contact-only view (if full SELECT * is too wide):
-- SELECT MR_NO, CONTACT_NO FROM PATIENT_INFORMATION WHERE MR_NO = '&mr_no';

SELECT *
FROM MOBILE_PATIENT_REGISTRATION
WHERE MR_NO = '&mr_no';

-- =============================================================================
-- C) Mobile portal tables used by the app (may return 0 rows — that is OK)
-- =============================================================================

-- Trusted devices (after verify-login-otp + trustDevice)
SELECT 'PATIENT_TRUSTED_DEVICE' AS src, t.*
FROM PATIENT_TRUSTED_DEVICE t
WHERE t.MR_NO = '&mr_no'
ORDER BY NVL(t.LAST_LOGIN_AT, t.TRUSTED_AT) DESC;

-- FCM device tokens
SELECT 'PATIENT_DEVICE_TOKEN' AS src, d.*
FROM PATIENT_DEVICE_TOKEN d
WHERE d.MR_NO = '&mr_no';

-- Notification inbox
SELECT 'PATIENT_NOTIFICATION' AS src, n.*
FROM PATIENT_NOTIFICATION n
WHERE n.MR_NO = '&mr_no'
ORDER BY n.CREATED_AT DESC;

-- App PIN
SELECT 'PATIENT_APP_PIN' AS src, p.*
FROM PATIENT_APP_PIN p
WHERE p.MR_NO = '&mr_no';

-- Recent activity
SELECT 'PATIENT_RECENT_ACTIVITY' AS src, a.*
FROM PATIENT_RECENT_ACTIVITY a
WHERE a.MR_NO = '&mr_no'
ORDER BY a.CREATED_AT DESC;

-- Profile photo metadata
SELECT 'PATIENT_PROFILE_PHOTO' AS src, ph.*
FROM PATIENT_PROFILE_PHOTO ph
WHERE ph.MR_NO = '&mr_no';

-- Messaging threads
SELECT 'PATIENT_MSG_THREAD' AS src, th.*
FROM PATIENT_MSG_THREAD th
WHERE th.MR_NO = '&mr_no'
ORDER BY th.CREATED_AT DESC;

-- Medication refill / reminders
SELECT 'PATIENT_MED_REFILL_REQUEST' AS src, r.*
FROM PATIENT_MED_REFILL_REQUEST r
WHERE r.MR_NO = '&mr_no';

SELECT 'PATIENT_MED_REMINDER' AS src, m.*
FROM PATIENT_MED_REMINDER m
WHERE m.MR_NO = '&mr_no';

-- Payments
SELECT 'MOBILE_PAYMENT_INTENT' AS src, i.*
FROM MOBILE_PAYMENT_INTENT i
WHERE i.MR_NO = '&mr_no'
ORDER BY i.CREATED_AT DESC;

-- Telemedicine
SELECT 'TELEMED_SESSION' AS src, s.*
FROM TELEMED_SESSION s
WHERE s.MR_NO = '&mr_no'
ORDER BY s.CREATED_AT DESC;

-- Support tickets
SELECT 'MOBILE_SUPPORT_TICKET' AS src, k.*
FROM MOBILE_SUPPORT_TICKET k
WHERE k.MR_NO = '&mr_no'
ORDER BY k.CREATED_AT DESC;

-- App analytics sessions
SELECT 'MOBILE_APP_SESSION' AS src, sess.*
FROM MOBILE_APP_SESSION sess
WHERE sess.MR_NO = '&mr_no'
ORDER BY sess.STARTED_AT DESC;

-- Audit log
SELECT 'MOBILE_AUDIT_LOG' AS src, au.*
FROM MOBILE_AUDIT_LOG au
WHERE au.MR_NO = '&mr_no'
ORDER BY au.CREATED_AT DESC;

-- =============================================================================
-- D) Presence summary — which tables contain this patient?
--     (Run each block; ORA-00942 = table not created yet — skip that one)
-- =============================================================================
SELECT 'PATIENT_MST' AS table_name, COUNT(*) AS row_count
FROM PATIENT_MST WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_INFORMATION', COUNT(*) FROM PATIENT_INFORMATION WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'MOBILE_PATIENT_REGISTRATION', COUNT(*) FROM MOBILE_PATIENT_REGISTRATION WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_TRUSTED_DEVICE', COUNT(*) FROM PATIENT_TRUSTED_DEVICE WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_DEVICE_TOKEN', COUNT(*) FROM PATIENT_DEVICE_TOKEN WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_NOTIFICATION', COUNT(*) FROM PATIENT_NOTIFICATION WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_APP_PIN', COUNT(*) FROM PATIENT_APP_PIN WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_RECENT_ACTIVITY', COUNT(*) FROM PATIENT_RECENT_ACTIVITY WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_PROFILE_PHOTO', COUNT(*) FROM PATIENT_PROFILE_PHOTO WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_MSG_THREAD', COUNT(*) FROM PATIENT_MSG_THREAD WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_MED_REFILL_REQUEST', COUNT(*) FROM PATIENT_MED_REFILL_REQUEST WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'PATIENT_MED_REMINDER', COUNT(*) FROM PATIENT_MED_REMINDER WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'MOBILE_PAYMENT_INTENT', COUNT(*) FROM MOBILE_PAYMENT_INTENT WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'TELEMED_SESSION', COUNT(*) FROM TELEMED_SESSION WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'MOBILE_SUPPORT_TICKET', COUNT(*) FROM MOBILE_SUPPORT_TICKET WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'MOBILE_APP_SESSION', COUNT(*) FROM MOBILE_APP_SESSION WHERE MR_NO = '&mr_no'
UNION ALL
SELECT 'MOBILE_AUDIT_LOG', COUNT(*) FROM MOBILE_AUDIT_LOG WHERE MR_NO = '&mr_no';

-- =============================================================================
-- E) Other patients (examples) — uncomment what you need
-- =============================================================================

-- All registered portal patients:
-- SELECT MR_NO, IS_ACTIVE, CREATED_AT
-- FROM MOBILE_PATIENT_REGISTRATION
-- ORDER BY CREATED_AT DESC;

-- All patients that have trusted a device (same table as this user):
-- SELECT DISTINCT MR_NO FROM PATIENT_TRUSTED_DEVICE ORDER BY MR_NO;

-- All patients with FCM tokens:
-- SELECT DISTINCT MR_NO FROM PATIENT_DEVICE_TOKEN ORDER BY MR_NO;

-- Find this patient's CONTACT_NO, then other MRs sharing that phone (rare):
-- SELECT pi2.MR_NO, pi2.CONTACT_NO
-- FROM PATIENT_INFORMATION pi1
-- JOIN PATIENT_INFORMATION pi2 ON pi2.CONTACT_NO = pi1.CONTACT_NO
-- WHERE pi1.MR_NO = '&mr_no';
