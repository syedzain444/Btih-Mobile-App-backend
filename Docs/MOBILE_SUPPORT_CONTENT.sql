-- Help & Support contact + FAQ (admin-managed)
-- Run as HMIS mobile-portal schema user if auto-ensure is unavailable.
-- Compatible with Oracle 11g / 12c+.

DECLARE
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'MOBILE_SUPPORT_CONTACT';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE TABLE MOBILE_SUPPORT_CONTACT (
          CONTACT_ID      NUMBER         DEFAULT 1 NOT NULL,
          HOSPITAL_NAME   VARCHAR2(200)  NOT NULL,
          PHONE           VARCHAR2(50)   NOT NULL,
          EMAIL           VARCHAR2(120)  NOT NULL,
          ADDRESS         VARCHAR2(400)  NOT NULL,
          WORKING_HOURS   VARCHAR2(200)  NOT NULL,
          UPDATED_AT      DATE           DEFAULT SYSDATE NOT NULL,
          CONSTRAINT PK_MOBILE_SUPPORT_CONTACT PRIMARY KEY (CONTACT_ID)
      )';
  END IF;

  SELECT COUNT(*) INTO v_count FROM user_sequences WHERE sequence_name = 'MOBILE_FAQ_SEQ';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE SEQUENCE MOBILE_FAQ_SEQ
          START WITH 1
          INCREMENT BY 1
          NOCACHE
          NOCYCLE';
  END IF;

  SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'MOBILE_FAQ';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE TABLE MOBILE_FAQ (
          FAQ_ID        NUMBER         NOT NULL,
          CATEGORY      VARCHAR2(80)   DEFAULT ''General'' NOT NULL,
          QUESTION_EN   VARCHAR2(500)  NOT NULL,
          ANSWER_EN     VARCHAR2(2000) NOT NULL,
          QUESTION_UR   VARCHAR2(500),
          ANSWER_UR     VARCHAR2(2000),
          SORT_ORDER    NUMBER         DEFAULT 0 NOT NULL,
          IS_ACTIVE     CHAR(1)        DEFAULT ''Y'' NOT NULL,
          CREATED_AT    DATE           DEFAULT SYSDATE NOT NULL,
          UPDATED_AT    DATE           DEFAULT SYSDATE NOT NULL,
          CONSTRAINT PK_MOBILE_FAQ PRIMARY KEY (FAQ_ID),
          CONSTRAINT CHK_MOBILE_FAQ_ACTIVE CHECK (IS_ACTIVE IN (''Y'', ''N''))
      )';

    EXECUTE IMMEDIATE '
      CREATE OR REPLACE TRIGGER TRG_MOBILE_FAQ_BI
      BEFORE INSERT ON MOBILE_FAQ
      FOR EACH ROW
      BEGIN
          IF :NEW.FAQ_ID IS NULL THEN
              SELECT MOBILE_FAQ_SEQ.NEXTVAL INTO :NEW.FAQ_ID FROM DUAL;
          END IF;
      END;';
  END IF;
END;
/

MERGE INTO MOBILE_SUPPORT_CONTACT t
USING (SELECT 1 AS CONTACT_ID FROM DUAL) s
   ON (t.CONTACT_ID = s.CONTACT_ID)
WHEN NOT MATCHED THEN
  INSERT (CONTACT_ID, HOSPITAL_NAME, PHONE, EMAIL, ADDRESS, WORKING_HOURS, UPDATED_AT)
  VALUES (
    1,
    'Bahria Town International Hospital',
    '03491660025',
    'info@btkhospital.com',
    'Bahria Town, Karachi',
    '24/7 Emergency | OPD 8:00 AM – 8:00 PM',
    SYSDATE
  );

COMMIT;
