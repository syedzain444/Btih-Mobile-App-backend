-- Sync APPOINTMENT_SEQ when it falls behind MAX(APPOINTMENT_ID).
-- Symptom: mobile booking returns 400 {"message":"Insertion failed"}
-- Root cause: ORA-00001 unique constraint (APPOINTMENT_PK) — trigger assigns a stale sequence value.
-- Run as HOSWEB_MVC_LIVE on deverp (172.16.10.73:1522).

DECLARE
  v_max  NUMBER;
  v_next NUMBER;
  v_diff NUMBER;
BEGIN
  SELECT NVL(MAX(APPOINTMENT_ID), 0) INTO v_max FROM APPOINTMENT;
  SELECT APPOINTMENT_SEQ.NEXTVAL INTO v_next FROM DUAL;
  v_diff := (v_max + 1) - v_next;

  IF v_diff > 0 THEN
    EXECUTE IMMEDIATE 'ALTER SEQUENCE APPOINTMENT_SEQ INCREMENT BY ' || v_diff;
    SELECT APPOINTMENT_SEQ.NEXTVAL INTO v_next FROM DUAL;
    EXECUTE IMMEDIATE 'ALTER SEQUENCE APPOINTMENT_SEQ INCREMENT BY 1';
    DBMS_OUTPUT.PUT_LINE('APPOINTMENT_SEQ advanced; next usable id around ' || TO_CHAR(v_next + 1));
  ELSE
    DBMS_OUTPUT.PUT_LINE('APPOINTMENT_SEQ already ahead of MAX(APPOINTMENT_ID)=' || v_max);
  END IF;
END;
/

-- Verify
SELECT (SELECT NVL(MAX(APPOINTMENT_ID), 0) FROM APPOINTMENT) AS max_id,
       APPOINTMENT_SEQ.NEXTVAL AS next_seq
  FROM DUAL;
