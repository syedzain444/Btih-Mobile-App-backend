-- Revoke existing trusted devices so next login requires OTP again.
-- Optional: run for the patient linked to 03339993577 (or any MR).

-- Find MR for the phone:
SELECT mr_no, contact_no
FROM patient_information
WHERE REPLACE(REPLACE(contact_no, ' ', ''), '-', '') LIKE '%3339993577%';

-- Then revoke (change MR No):
-- UPDATE patient_trusted_device
-- SET is_active = 'N', expires_at = SYSDATE
-- WHERE mr_no = '010-027-090';
