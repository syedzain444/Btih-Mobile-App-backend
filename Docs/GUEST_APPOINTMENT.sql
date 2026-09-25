-- Guest appointment bookings for the mobile walk-in flow.
-- Run as the HMIS mobile-portal schema user (same as GUEST_PATIENT).
-- Compatible with Oracle 11g / 12c+ (sequence + trigger — no IDENTITY column).
-- Safe to re-run: skips objects that already exist.

DECLARE
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM user_sequences WHERE sequence_name = 'GUEST_APPOINTMENT_SEQ';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE SEQUENCE GUEST_APPOINTMENT_SEQ
          START WITH 1
          INCREMENT BY 1
          NOCACHE
          NOCYCLE';
  END IF;

  SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'GUEST_APPOINTMENT';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE TABLE GUEST_APPOINTMENT (
          GUEST_APPOINTMENT_ID NUMBER        NOT NULL,
          GUEST_ID             NUMBER,
          MOBILE_NUMBER        VARCHAR2(20)  NOT NULL,
          FULL_NAME            VARCHAR2(120) NOT NULL,
          DOCTOR_ID            NUMBER        NOT NULL,
          DOCTOR_NAME          VARCHAR2(120),
          DEPARTMENT_ID        NUMBER,
          WEEK_ID              NUMBER,
          APPOINTMENT_TIME     VARCHAR2(80)  NOT NULL,
          STATUS               VARCHAR2(30)  DEFAULT ''Pending'' NOT NULL,
          PURPOSE              VARCHAR2(200),
          HMIS_APPOINTMENT_ID  VARCHAR2(40),
          CANCEL_REASON        VARCHAR2(500),
          CREATED_AT           DATE          DEFAULT SYSDATE NOT NULL,
          UPDATED_AT           DATE          DEFAULT SYSDATE NOT NULL,
          IS_ACTIVE            CHAR(1)       DEFAULT ''Y'' NOT NULL,
          CONSTRAINT PK_GUEST_APPOINTMENT PRIMARY KEY (GUEST_APPOINTMENT_ID),
          CONSTRAINT CHK_GUEST_APPT_ACTIVE CHECK (IS_ACTIVE IN (''Y'', ''N''))
      )';
  END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_GUEST_APPOINTMENT_BI
BEFORE INSERT ON GUEST_APPOINTMENT
FOR EACH ROW
BEGIN
    IF :NEW.GUEST_APPOINTMENT_ID IS NULL THEN
        SELECT GUEST_APPOINTMENT_SEQ.NEXTVAL
          INTO :NEW.GUEST_APPOINTMENT_ID
          FROM DUAL;
    END IF;
END;
/

BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX IDX_GUEST_APPT_MOBILE ON GUEST_APPOINTMENT (MOBILE_NUMBER, CREATED_AT DESC)';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -955 THEN RAISE; END IF;
END;
/

BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX IDX_GUEST_APPT_GUEST ON GUEST_APPOINTMENT (GUEST_ID, IS_ACTIVE)';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -955 THEN RAISE; END IF;
END;
/

COMMENT ON TABLE GUEST_APPOINTMENT IS
    'Guest walk-in appointments saved by the mobile app (alongside optional HMIS APPOINTMENT challan).';

SELECT COUNT(*) AS guest_appointment_ready
FROM user_tables
WHERE table_name = 'GUEST_APPOINTMENT';
