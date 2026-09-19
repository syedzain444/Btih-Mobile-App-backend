-- Canonical notification types for PATIENT_NOTIFICATION.NOTIFICATION_TYPE
-- Categories: appointments | medications | lab | records | billing | messaging | security | general

-- Appointments
-- appointment_request_received
-- appointment_confirmed
-- appointment_cancelled
-- appointment_rescheduled
-- appointment_reminder
-- follow_up_reminder

-- Lab / records
-- lab_report_ready
-- gastro_report_ready
-- radiology_report_ready
-- prescription_added
-- discharge_summary_ready
-- visit_summary_ready
-- report_ready (legacy generic)

-- Medications
-- medication_reminder
-- medication_schedule_updated

-- Billing
-- bill_generated
-- payment_pending
-- payment_confirmed

-- Messaging
-- message_received
-- message_thread_closed

-- Security
-- profile_updated
-- password_changed
-- app_pin_changed
-- trusted_device_added
-- trusted_device_removed
-- new_login_alert

-- General
-- hospital_announcement
-- hospital_promotion

-- Example: list inbox with date/time
SELECT
    NOTIFICATION_ID,
    MR_NO,
    NOTIFICATION_TYPE,
    CATEGORY,
    PRIORITY,
    TITLE,
    BODY,
    IS_READ,
    TO_CHAR(CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') AS CREATED_AT,
    TO_CHAR(READ_AT, 'YYYY-MM-DD HH24:MI:SS') AS READ_AT
FROM PATIENT_NOTIFICATION
WHERE MR_NO = '010-002-152'
ORDER BY CREATED_AT DESC;
