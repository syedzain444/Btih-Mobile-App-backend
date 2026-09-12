-- Guest walk-in profile for mobile app (no MR number).
-- Run as the HMIS mobile-portal schema user (same as PATIENT_DEVICE_TOKEN).
-- Compatible with Oracle 11g / 12c+ (sequence + trigger — no IDENTITY column).

CREATE SEQUENCE GUEST_PATIENT_SEQ
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

CREATE TABLE GUEST_PATIENT (
    GUEST_ID          NUMBER        NOT NULL,
    FULL_NAME         VARCHAR2(120) NOT NULL,
    MOBILE_NUMBER     VARCHAR2(20)  NOT NULL,
    DATE_OF_BIRTH     DATE          NOT NULL,
    GENDER            VARCHAR2(20)  NOT NULL,
    CREATED_AT        DATE          DEFAULT SYSDATE NOT NULL,
    UPDATED_AT        DATE          DEFAULT SYSDATE NOT NULL,
    IS_ACTIVE         CHAR(1)       DEFAULT 'Y' NOT NULL,
    CONSTRAINT PK_GUEST_PATIENT PRIMARY KEY (GUEST_ID),
    CONSTRAINT CHK_GUEST_PATIENT_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
);

CREATE OR REPLACE TRIGGER TRG_GUEST_PATIENT_BI
BEFORE INSERT ON GUEST_PATIENT
FOR EACH ROW
BEGIN
    IF :NEW.GUEST_ID IS NULL THEN
        SELECT GUEST_PATIENT_SEQ.NEXTVAL
          INTO :NEW.GUEST_ID
          FROM DUAL;
    END IF;
END;
/

CREATE UNIQUE INDEX GUEST_PATIENT_MOBILE_UK
    ON GUEST_PATIENT (MOBILE_NUMBER);

CREATE INDEX GUEST_PATIENT_ACTIVE_IDX
    ON GUEST_PATIENT (IS_ACTIVE, UPDATED_AT DESC);

COMMENT ON TABLE GUEST_PATIENT IS
    'Guest profile collected before doctor search / appointment booking in walk-in mode.';
