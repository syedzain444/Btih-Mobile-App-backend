-- Offers & Packages catalog for the mobile app (admin-managed).
-- Run as HMIS mobile-portal schema user (HMISConnection).
-- Compatible with Oracle 11g / 12c+.

DECLARE
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM user_sequences WHERE sequence_name = 'MOBILE_OFFER_SEQ';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE SEQUENCE MOBILE_OFFER_SEQ
          START WITH 1
          INCREMENT BY 1
          NOCACHE
          NOCYCLE';
  END IF;

  SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'MOBILE_OFFER';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE '
      CREATE TABLE MOBILE_OFFER (
          OFFER_ID         NUMBER          NOT NULL,
          TITLE            VARCHAR2(160)   NOT NULL,
          SUBTITLE         VARCHAR2(200),
          DESCRIPTION      VARCHAR2(2000),
          CATEGORY         VARCHAR2(60)    DEFAULT ''Package'' NOT NULL,
          IMAGE_URL        VARCHAR2(500),
          ORIGINAL_PRICE   NUMBER,
          OFFER_PRICE      NUMBER,
          CURRENCY         VARCHAR2(10)    DEFAULT ''PKR'' NOT NULL,
          HIGHLIGHTS       VARCHAR2(2000),
          CTA_LABEL        VARCHAR2(60)    DEFAULT ''Enquire'' NOT NULL,
          CTA_PHONE        VARCHAR2(30),
          SORT_ORDER       NUMBER          DEFAULT 0 NOT NULL,
          IS_ACTIVE        CHAR(1)         DEFAULT ''Y'' NOT NULL,
          START_AT         DATE,
          END_AT           DATE,
          CREATED_AT       DATE            DEFAULT SYSDATE NOT NULL,
          UPDATED_AT       DATE            DEFAULT SYSDATE NOT NULL,
          CONSTRAINT PK_MOBILE_OFFER PRIMARY KEY (OFFER_ID),
          CONSTRAINT CHK_MOBILE_OFFER_ACTIVE CHECK (IS_ACTIVE IN (''Y'', ''N''))
      )';
  END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_MOBILE_OFFER_BI
BEFORE INSERT ON MOBILE_OFFER
FOR EACH ROW
BEGIN
    IF :NEW.OFFER_ID IS NULL THEN
        SELECT MOBILE_OFFER_SEQ.NEXTVAL
          INTO :NEW.OFFER_ID
          FROM DUAL;
    END IF;
END;
/

BEGIN
  EXECUTE IMMEDIATE 'CREATE INDEX IDX_MOBILE_OFFER_ACTIVE ON MOBILE_OFFER (IS_ACTIVE, SORT_ORDER, START_AT, END_AT)';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -955 THEN RAISE; END IF;
END;
/

COMMENT ON TABLE MOBILE_OFFER IS
  'Health packages / offers managed in admin panel and shown in the mobile app More hub.';

SELECT COUNT(*) AS mobile_offer_ready
FROM user_tables
WHERE table_name = 'MOBILE_OFFER';
