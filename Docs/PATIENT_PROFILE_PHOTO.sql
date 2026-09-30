-- Patient profile photo storage (auto-created by API on startup / first upload).
-- Run manually only if schema auto-ensure is unavailable.

DECLARE
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM user_sequences WHERE sequence_name = 'PATIENT_PROFILE_PHOTO_SEQ';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE SEQUENCE PATIENT_PROFILE_PHOTO_SEQ
          START WITH 1
          INCREMENT BY 1
          NOCACHE
          NOCYCLE';
  END IF;

  SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'PATIENT_PROFILE_PHOTO';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE TABLE PATIENT_PROFILE_PHOTO (
          PHOTO_ID        NUMBER         NOT NULL,
          MR_NO           VARCHAR2(20)   NOT NULL,
          IMAGE_PATH      VARCHAR2(500)  NOT NULL,
          CONTENT_TYPE    VARCHAR2(100),
          FILE_SIZE       NUMBER,
          CREATED_AT      DATE           DEFAULT SYSDATE NOT NULL,
          UPDATED_AT      DATE           DEFAULT SYSDATE NOT NULL,
          IS_ACTIVE       CHAR(1)        DEFAULT ''Y'' NOT NULL,
          CONSTRAINT PK_PATIENT_PROFILE_PHOTO PRIMARY KEY (PHOTO_ID),
          CONSTRAINT CHK_PAT_PROF_PHOTO_ACTIVE CHECK (IS_ACTIVE IN (''Y'', ''N''))
      )';

    EXECUTE IMMEDIATE '
      CREATE OR REPLACE TRIGGER TRG_PATIENT_PROFILE_PHOTO_BI
      BEFORE INSERT ON PATIENT_PROFILE_PHOTO
      FOR EACH ROW
      BEGIN
          IF :NEW.PHOTO_ID IS NULL THEN
              SELECT PATIENT_PROFILE_PHOTO_SEQ.NEXTVAL
                INTO :NEW.PHOTO_ID
                FROM DUAL;
          END IF;
      END;';

    BEGIN
      EXECUTE IMMEDIATE '
        CREATE UNIQUE INDEX PATIENT_PROFILE_PHOTO_MR_UK
            ON PATIENT_PROFILE_PHOTO (MR_NO)';
    EXCEPTION
      WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF;
    END;
  END IF;
END;
/

COMMIT;
