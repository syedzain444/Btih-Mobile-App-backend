-- Quick check for guest appointment rows
SELECT USER AS connected_schema FROM DUAL;

SELECT table_name FROM user_tables
WHERE table_name IN ('GUEST_PATIENT', 'GUEST_APPOINTMENT')
ORDER BY table_name;

SELECT guest_appointment_id, guest_id, mobile_number, full_name,
       doctor_id, doctor_name, appointment_time, status, created_at
FROM guest_appointment
ORDER BY created_at DESC;
