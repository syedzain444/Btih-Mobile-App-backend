-- App launch promotions (splash → promotion carousel → welcome).
-- Run as HMIS mobile-portal schema user.
-- Compatible with Oracle 11g / 12c+.

DECLARE
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM user_sequences WHERE sequence_name = 'MOBILE_PROMOTION_SEQ';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE SEQUENCE MOBILE_PROMOTION_SEQ
          START WITH 1
          INCREMENT BY 1
          NOCACHE
          NOCYCLE';
  END IF;

  SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'MOBILE_PROMOTION';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE TABLE MOBILE_PROMOTION (
          PROMOTION_ID      NUMBER         NOT NULL,
          TITLE             VARCHAR2(120)  NOT NULL,
          IMAGE_URL         VARCHAR2(500)  NOT NULL,
          SORT_ORDER        NUMBER         DEFAULT 0 NOT NULL,
          DURATION_SECONDS  NUMBER         DEFAULT 5 NOT NULL,
          IS_ACTIVE         CHAR(1)        DEFAULT ''Y'' NOT NULL,
          START_AT          DATE,
          END_AT            DATE,
          CREATED_AT        DATE           DEFAULT SYSDATE NOT NULL,
          UPDATED_AT        DATE           DEFAULT SYSDATE NOT NULL,
          CONSTRAINT PK_MOBILE_PROMOTION PRIMARY KEY (PROMOTION_ID),
          CONSTRAINT CHK_MOBILE_PROMO_ACTIVE CHECK (IS_ACTIVE IN (''Y'', ''N''))
      )';
  END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_MOBILE_PROMOTION_BI
BEFORE INSERT ON MOBILE_PROMOTION
FOR EACH ROW
BEGIN
    IF :NEW.PROMOTION_ID IS NULL THEN
        SELECT MOBILE_PROMOTION_SEQ.NEXTVAL
          INTO :NEW.PROMOTION_ID
          FROM DUAL;
    END IF;
END;
/

BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX IDX_MOBILE_PROMO_ACTIVE ON MOBILE_PROMOTION (IS_ACTIVE, SORT_ORDER, START_AT, END_AT)';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -955 THEN RAISE; END IF;
END;
/

COMMENT ON TABLE MOBILE_PROMOTION IS
    'Launch-screen promotions managed by admin panel and shown after splash in the mobile app.';

SELECT COUNT(*) AS mobile_promotion_ready
FROM user_tables
WHERE table_name = 'MOBILE_PROMOTION';
