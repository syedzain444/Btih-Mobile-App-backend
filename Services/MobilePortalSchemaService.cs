using HospitalMobileAPPApi.Helpers;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Services
{
    public interface IMobilePortalSchemaService
    {
        IReadOnlyList<string> RequiredTables { get; }
        Task<IReadOnlyList<string>> GetMissingTablesAsync();
        Task<IReadOnlyList<ConnectedDatabaseInfo>> GetConnectedDatabasesAsync();
        Task EnsurePromotionSchemaAsync(CancellationToken cancellationToken = default);
        Task EnsureOfferSchemaAsync(CancellationToken cancellationToken = default);
        Task EnsureAdminRbacSchemaAsync(CancellationToken cancellationToken = default);
        Task EnsureProfilePhotoSchemaAsync(CancellationToken cancellationToken = default);
        Task EnsureSupportContentSchemaAsync(CancellationToken cancellationToken = default);
    }

    public sealed class ConnectedDatabaseInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public bool Configured { get; init; }
        public bool Connected { get; init; }
        public string Status { get; init; } = "unknown";
        public int? LatencyMs { get; init; }
        public string? Error { get; init; }

        public string? UserId { get; init; }
        public string? Host { get; init; }
        public int? Port { get; init; }
        public string? ServiceName { get; init; }
        public string? DataSource { get; init; }

        public string? SessionUser { get; init; }
        public string? CurrentSchema { get; init; }
        public string? DbName { get; init; }
        public string? InstanceName { get; init; }
        public string? ServerHost { get; init; }
        public string? ServerServiceName { get; init; }
        public string? ServerTime { get; init; }
        public string? OracleBanner { get; init; }
    }

    public class MobilePortalSchemaService : IMobilePortalSchemaService
    {
        private static readonly string[] RequiredTablesList =
        {
            "PATIENT_MSG_THREAD",
            "PATIENT_MSG",
            "PATIENT_MSG_ATTACHMENT",
            "MOBILE_PATIENT_REGISTRATION",
            "PATIENT_MED_REFILL_REQUEST",
            "PATIENT_MED_REMINDER",
            "PATIENT_DEVICE_TOKEN",
            "PATIENT_NOTIFICATION",
            "PATIENT_RECENT_ACTIVITY",
            "PATIENT_TRUSTED_DEVICE",
            "PATIENT_APP_PIN",
            "GUEST_PATIENT",
            "GUEST_APPOINTMENT",
            "PATIENT_PROFILE_PHOTO",
            "MOBILE_PAYMENT_INTENT",
            "MOBILE_PAYMENT_TRANSACTION",
            "MOBILE_PAYMENT_QR",
            "MOBILE_ADMIN_USER",
            "MOBILE_ADMIN_ROLE",
            "MOBILE_ADMIN_PERMISSION",
            "MOBILE_ADMIN_ROLE_PERM",
            "MOBILE_AUDIT_LOG",
            "TELEMED_SESSION",
            "MOBILE_CONTENT_LOCALIZED",
            "MOBILE_PROMOTION",
            "MOBILE_OFFER",
            "MOBILE_FAQ",
            "MOBILE_SUPPORT_TICKET",
            "MOBILE_APP_SESSION",
            "MOBILE_APPOINTMENT_PREP_ALERT",
        };

        public IReadOnlyList<string> RequiredTables => RequiredTablesList;

        private static readonly (string Name, string Purpose)[] ConnectionTargets =
        {
            ("HMISConnection", "Primary patient / mobile portal schema"),
            ("HOS_WEB_MVC_LIVE", "Doctors, schedules, and related hospital web data"),
        };

        private readonly IConfiguration _configuration;

        public MobilePortalSchemaService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<string>> GetMissingTablesAsync()
        {
            var missing = new List<string>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            if (string.IsNullOrWhiteSpace(connStr))
            {
                return RequiredTablesList;
            }

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            foreach (var table in RequiredTablesList)
            {
                await using var cmd = new OracleCommand(@"
                    SELECT COUNT(*)
                    FROM USER_TABLES
                    WHERE TABLE_NAME = :table_name", conn);

                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter("table_name", table));

                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    missing.Add(table);
                }
            }

            return missing;
        }

        public async Task<IReadOnlyList<ConnectedDatabaseInfo>> GetConnectedDatabasesAsync()
        {
            var results = new List<ConnectedDatabaseInfo>(ConnectionTargets.Length);
            foreach (var (name, purpose) in ConnectionTargets)
            {
                results.Add(await ProbeConnectionAsync(name, purpose));
            }

            return results;
        }

        /// <summary>
        /// Creates MOBILE_PROMOTION table/sequence and display-limit settings if missing.
        /// </summary>
        public async Task EnsurePromotionSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                return;
            }

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(conn, "MOBILE_PROMOTION", cancellationToken))
            {
                if (!await SequenceExistsAsync(conn, "MOBILE_PROMOTION_SEQ", cancellationToken))
                {
                    await ExecuteDdlAsync(conn, @"
                        CREATE SEQUENCE MOBILE_PROMOTION_SEQ
                            START WITH 1
                            INCREMENT BY 1
                            NOCACHE
                            NOCYCLE", cancellationToken);
                }

                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_PROMOTION (
                        PROMOTION_ID      NUMBER         NOT NULL,
                        TITLE             VARCHAR2(120)  NOT NULL,
                        IMAGE_URL         VARCHAR2(500)  NOT NULL,
                        SORT_ORDER        NUMBER         DEFAULT 0 NOT NULL,
                        DURATION_SECONDS  NUMBER         DEFAULT 5 NOT NULL,
                        IS_ACTIVE         CHAR(1)        DEFAULT 'Y' NOT NULL,
                        START_AT          DATE,
                        END_AT            DATE,
                        CREATED_AT        DATE           DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT        DATE           DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_PROMOTION PRIMARY KEY (PROMOTION_ID),
                        CONSTRAINT CHK_MOBILE_PROMO_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
                    )", cancellationToken);

                await ExecuteDdlAsync(conn, @"
                    CREATE OR REPLACE TRIGGER TRG_MOBILE_PROMOTION_BI
                    BEFORE INSERT ON MOBILE_PROMOTION
                    FOR EACH ROW
                    BEGIN
                        IF :NEW.PROMOTION_ID IS NULL THEN
                            SELECT MOBILE_PROMOTION_SEQ.NEXTVAL
                              INTO :NEW.PROMOTION_ID
                              FROM DUAL;
                        END IF;
                    END;", cancellationToken);

                try
                {
                    await ExecuteDdlAsync(conn, @"
                        CREATE INDEX IDX_MOBILE_PROMO_ACTIVE
                            ON MOBILE_PROMOTION (IS_ACTIVE, SORT_ORDER, START_AT, END_AT)", cancellationToken);
                }
                catch (OracleException ex) when (ex.Number == 955)
                {
                    // Index already exists.
                }
            }

            await EnsurePromotionSettingsAsync(conn, cancellationToken);
        }

        /// <summary>
        /// Creates MOBILE_OFFER table/sequence if missing (offers &amp; packages catalog).
        /// </summary>
        public async Task EnsureOfferSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                return;
            }

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(conn, "MOBILE_OFFER", cancellationToken))
            {
                if (!await SequenceExistsAsync(conn, "MOBILE_OFFER_SEQ", cancellationToken))
                {
                    await ExecuteDdlAsync(conn, @"
                        CREATE SEQUENCE MOBILE_OFFER_SEQ
                            START WITH 1
                            INCREMENT BY 1
                            NOCACHE
                            NOCYCLE", cancellationToken);
                }

                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_OFFER (
                        OFFER_ID         NUMBER          NOT NULL,
                        TITLE            VARCHAR2(160)   NOT NULL,
                        SUBTITLE         VARCHAR2(200),
                        DESCRIPTION      VARCHAR2(2000),
                        CATEGORY         VARCHAR2(60)    DEFAULT 'Package' NOT NULL,
                        IMAGE_URL        VARCHAR2(500),
                        ORIGINAL_PRICE   NUMBER,
                        OFFER_PRICE      NUMBER,
                        CURRENCY         VARCHAR2(10)    DEFAULT 'PKR' NOT NULL,
                        HIGHLIGHTS       VARCHAR2(2000),
                        CTA_LABEL        VARCHAR2(60)    DEFAULT 'Enquire' NOT NULL,
                        CTA_PHONE        VARCHAR2(30),
                        SORT_ORDER       NUMBER          DEFAULT 0 NOT NULL,
                        IS_ACTIVE        CHAR(1)         DEFAULT 'Y' NOT NULL,
                        START_AT         DATE,
                        END_AT           DATE,
                        CREATED_AT       DATE            DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT       DATE            DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_OFFER PRIMARY KEY (OFFER_ID),
                        CONSTRAINT CHK_MOBILE_OFFER_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
                    )", cancellationToken);

                await ExecuteDdlAsync(conn, @"
                    CREATE OR REPLACE TRIGGER TRG_MOBILE_OFFER_BI
                    BEFORE INSERT ON MOBILE_OFFER
                    FOR EACH ROW
                    BEGIN
                        IF :NEW.OFFER_ID IS NULL THEN
                            SELECT MOBILE_OFFER_SEQ.NEXTVAL
                              INTO :NEW.OFFER_ID
                              FROM DUAL;
                        END IF;
                    END;", cancellationToken);

                try
                {
                    await ExecuteDdlAsync(conn, @"
                        CREATE INDEX IDX_MOBILE_OFFER_ACTIVE
                            ON MOBILE_OFFER (IS_ACTIVE, SORT_ORDER, START_AT, END_AT)", cancellationToken);
                }
                catch (OracleException ex) when (ex.Number == 955)
                {
                    // Index already exists.
                }
            }
            else if (!await SequenceExistsAsync(conn, "MOBILE_OFFER_SEQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE SEQUENCE MOBILE_OFFER_SEQ
                        START WITH 1
                        INCREMENT BY 1
                        NOCACHE
                        NOCYCLE", cancellationToken);
            }
        }

        /// <summary>
        /// Creates admin user / role / permission tables and seeds default RBAC matrix.
        /// </summary>
        public async Task EnsureAdminRbacSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            if (string.IsNullOrWhiteSpace(connStr)) return;

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            await EnsureAdminUserInfrastructureAsync(conn, cancellationToken);

            if (!await SequenceExistsAsync(conn, "MOBILE_ADMIN_ROLE_SEQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE SEQUENCE MOBILE_ADMIN_ROLE_SEQ
                        START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE", cancellationToken);
            }

            if (!await SequenceExistsAsync(conn, "MOBILE_ADMIN_PERM_SEQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE SEQUENCE MOBILE_ADMIN_PERM_SEQ
                        START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_ADMIN_ROLE", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_ADMIN_ROLE (
                        ROLE_ID       NUMBER         NOT NULL,
                        CODE          VARCHAR2(40)   NOT NULL,
                        NAME          VARCHAR2(80)   NOT NULL,
                        DESCRIPTION   VARCHAR2(400),
                        IS_SYSTEM     CHAR(1)        DEFAULT 'N' NOT NULL,
                        IS_ACTIVE     CHAR(1)        DEFAULT 'Y' NOT NULL,
                        CREATED_AT    DATE           DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT    DATE           DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_ADMIN_ROLE PRIMARY KEY (ROLE_ID),
                        CONSTRAINT UK_MOBILE_ADMIN_ROLE_CODE UNIQUE (CODE),
                        CONSTRAINT CHK_MOBILE_ADMIN_ROLE_SYS CHECK (IS_SYSTEM IN ('Y','N')),
                        CONSTRAINT CHK_MOBILE_ADMIN_ROLE_ACT CHECK (IS_ACTIVE IN ('Y','N'))
                    )", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_ADMIN_PERMISSION", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_ADMIN_PERMISSION (
                        PERMISSION_ID NUMBER         NOT NULL,
                        MODULE_KEY    VARCHAR2(60)   NOT NULL,
                        NAME          VARCHAR2(120)  NOT NULL,
                        DESCRIPTION   VARCHAR2(400),
                        SORT_ORDER    NUMBER         DEFAULT 0 NOT NULL,
                        CONSTRAINT PK_MOBILE_ADMIN_PERM PRIMARY KEY (PERMISSION_ID),
                        CONSTRAINT UK_MOBILE_ADMIN_PERM_KEY UNIQUE (MODULE_KEY)
                    )", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_ADMIN_ROLE_PERM", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_ADMIN_ROLE_PERM (
                        ROLE_ID       NUMBER NOT NULL,
                        PERMISSION_ID NUMBER NOT NULL,
                        CONSTRAINT PK_MOBILE_ADMIN_ROLE_PERM PRIMARY KEY (ROLE_ID, PERMISSION_ID),
                        CONSTRAINT FK_MARP_ROLE FOREIGN KEY (ROLE_ID)
                            REFERENCES MOBILE_ADMIN_ROLE (ROLE_ID),
                        CONSTRAINT FK_MARP_PERM FOREIGN KEY (PERMISSION_ID)
                            REFERENCES MOBILE_ADMIN_PERMISSION (PERMISSION_ID)
                    )", cancellationToken);
            }

            await SeedAdminRbacAsync(conn, cancellationToken);
        }

        private async Task EnsureAdminUserInfrastructureAsync(
            OracleConnection conn,
            CancellationToken cancellationToken)
        {
            if (!await SequenceExistsAsync(conn, "MOBILE_ADMIN_USER_SEQ", cancellationToken))
            {
                var startWith = 1;
                if (await TableExistsAsync(conn, "MOBILE_ADMIN_USER", cancellationToken))
                {
                    await using var maxCmd = new OracleCommand(
                        "SELECT NVL(MAX(ADMIN_ID), 0) + 1 FROM MOBILE_ADMIN_USER", conn);
                    startWith = Convert.ToInt32(await maxCmd.ExecuteScalarAsync(cancellationToken));
                    if (startWith < 1) startWith = 1;
                }

                await ExecuteDdlAsync(conn, $@"
                    CREATE SEQUENCE MOBILE_ADMIN_USER_SEQ
                        START WITH {startWith} INCREMENT BY 1 NOCACHE NOCYCLE", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_ADMIN_USER", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_ADMIN_USER (
                        ADMIN_ID       NUMBER         NOT NULL,
                        USERNAME       VARCHAR2(80)   NOT NULL,
                        DISPLAY_NAME   VARCHAR2(120),
                        PASSWORD_HASH  VARCHAR2(128)  NOT NULL,
                        ROLE           VARCHAR2(40)   DEFAULT 'Staff' NOT NULL,
                        IS_ACTIVE      CHAR(1)        DEFAULT 'Y' NOT NULL,
                        CREATED_AT     DATE           DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT     DATE           DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_ADMIN_USER PRIMARY KEY (ADMIN_ID),
                        CONSTRAINT UK_MOBILE_ADMIN_USER_NAME UNIQUE (USERNAME),
                        CONSTRAINT CHK_MOBILE_ADMIN_USER_ACT CHECK (IS_ACTIVE IN ('Y','N'))
                    )", cancellationToken);

                await ExecuteDdlAsync(conn, @"
                    CREATE OR REPLACE TRIGGER TRG_MOBILE_ADMIN_USER_BI
                    BEFORE INSERT ON MOBILE_ADMIN_USER
                    FOR EACH ROW
                    BEGIN
                        IF :NEW.ADMIN_ID IS NULL THEN
                            SELECT MOBILE_ADMIN_USER_SEQ.NEXTVAL INTO :NEW.ADMIN_ID FROM DUAL;
                        END IF;
                    END;", cancellationToken);
            }
            else
            {
                // Best-effort: ensure CREATED_AT / UPDATED_AT exist on older installs.
                // Check dictionary first — ALTER TABLE takes locks and can throw ORA-00054 under load.
                if (!await ColumnExistsAsync(conn, "MOBILE_ADMIN_USER", "CREATED_AT", cancellationToken))
                {
                    try
                    {
                        await ExecuteDdlAsync(conn,
                            "ALTER TABLE MOBILE_ADMIN_USER ADD CREATED_AT DATE DEFAULT SYSDATE",
                            cancellationToken);
                    }
                    catch (OracleException ex) when (ex.Number is 1430 or 01430 or 54 or 00054)
                    {
                        // Column exists or table locked — safe to continue.
                    }
                }

                if (!await ColumnExistsAsync(conn, "MOBILE_ADMIN_USER", "UPDATED_AT", cancellationToken))
                {
                    try
                    {
                        await ExecuteDdlAsync(conn,
                            "ALTER TABLE MOBILE_ADMIN_USER ADD UPDATED_AT DATE DEFAULT SYSDATE",
                            cancellationToken);
                    }
                    catch (OracleException ex) when (ex.Number is 1430 or 01430 or 54 or 00054)
                    {
                    }
                }
            }
        }

        private async Task SeedAdminRbacAsync(OracleConnection conn, CancellationToken cancellationToken)
        {
            foreach (var (key, name, description, sort) in AdminModules.Catalog)
            {
                await using var check = new OracleCommand(@"
                    SELECT COUNT(*) FROM MOBILE_ADMIN_PERMISSION WHERE UPPER(MODULE_KEY) = UPPER(:key)", conn);
                check.BindByName = true;
                check.Parameters.Add("key", OracleDbType.Varchar2).Value = key;
                var exists = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) > 0;
                if (exists) continue;

                int id;
                await using (var seq = new OracleCommand("SELECT MOBILE_ADMIN_PERM_SEQ.NEXTVAL FROM DUAL", conn))
                {
                    id = Convert.ToInt32((await seq.ExecuteScalarAsync(cancellationToken))!.ToString());
                }

                await using var ins = new OracleCommand(@"
                    INSERT INTO MOBILE_ADMIN_PERMISSION (PERMISSION_ID, MODULE_KEY, NAME, DESCRIPTION, SORT_ORDER)
                    VALUES (:id, :key, :name, :description, :sort_order)", conn);
                ins.BindByName = true;
                ins.Parameters.Add("id", OracleDbType.Int32).Value = id;
                ins.Parameters.Add("key", OracleDbType.Varchar2).Value = key;
                ins.Parameters.Add("name", OracleDbType.Varchar2).Value = name;
                ins.Parameters.Add("description", OracleDbType.Varchar2).Value = description;
                ins.Parameters.Add("sort_order", OracleDbType.Int32).Value = sort;
                await ins.ExecuteNonQueryAsync(cancellationToken);
            }

            await SeedRoleIfMissingAsync(conn, AppRoles.Admin, "Administrator",
                "Full access including access control", true, AdminModules.Catalog.Select(c => c.Key).ToArray(),
                cancellationToken);
            await SeedRoleIfMissingAsync(conn, AppRoles.Staff, "Staff",
                "Operations: refills, tickets, promotions, offers, help content", true,
                [
                    AdminModules.Dashboard, AdminModules.Messages, AdminModules.Appointments,
                    AdminModules.Refills, AdminModules.Tickets, AdminModules.Promotions,
                    AdminModules.Offers, AdminModules.SupportContent,
                ], cancellationToken);
            await SeedRoleIfMissingAsync(conn, AppRoles.Reception, "Reception",
                "Front desk: dashboard, messages, appointments", true,
                [AdminModules.Dashboard, AdminModules.Messages, AdminModules.Appointments],
                cancellationToken);
        }

        private static async Task SeedRoleIfMissingAsync(
            OracleConnection conn,
            string code,
            string name,
            string description,
            bool isSystem,
            string[] modules,
            CancellationToken cancellationToken)
        {
            await using var check = new OracleCommand(@"
                SELECT ROLE_ID FROM MOBILE_ADMIN_ROLE WHERE UPPER(CODE) = UPPER(:code)", conn);
            check.BindByName = true;
            check.Parameters.Add("code", OracleDbType.Varchar2).Value = code;
            var existingId = await check.ExecuteScalarAsync(cancellationToken);
            int roleId;
            if (existingId == null || existingId == DBNull.Value)
            {
                await using (var seq = new OracleCommand("SELECT MOBILE_ADMIN_ROLE_SEQ.NEXTVAL FROM DUAL", conn))
                {
                    roleId = Convert.ToInt32((await seq.ExecuteScalarAsync(cancellationToken))!.ToString());
                }

                await using var ins = new OracleCommand(@"
                    INSERT INTO MOBILE_ADMIN_ROLE (
                        ROLE_ID, CODE, NAME, DESCRIPTION, IS_SYSTEM, IS_ACTIVE, CREATED_AT, UPDATED_AT
                    ) VALUES (
                        :id, :code, :name, :description, :is_system, 'Y', SYSDATE, SYSDATE
                    )", conn);
                ins.BindByName = true;
                ins.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
                ins.Parameters.Add("code", OracleDbType.Varchar2).Value = code;
                ins.Parameters.Add("name", OracleDbType.Varchar2).Value = name;
                ins.Parameters.Add("description", OracleDbType.Varchar2).Value = description;
                ins.Parameters.Add("is_system", OracleDbType.Char).Value = isSystem ? "Y" : "N";
                await ins.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                roleId = Convert.ToInt32(existingId);
            }

            // Seed permissions only when role currently has none (preserve admin customizations)
            await using var countCmd = new OracleCommand(
                "SELECT COUNT(*) FROM MOBILE_ADMIN_ROLE_PERM WHERE ROLE_ID = :id", conn);
            countCmd.BindByName = true;
            countCmd.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
            var permCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));
            if (permCount > 0) return;

            foreach (var module in modules)
            {
                await using var link = new OracleCommand(@"
                    INSERT INTO MOBILE_ADMIN_ROLE_PERM (ROLE_ID, PERMISSION_ID)
                    SELECT :role_id, PERMISSION_ID FROM MOBILE_ADMIN_PERMISSION
                    WHERE UPPER(MODULE_KEY) = UPPER(:module_key)", conn);
                link.BindByName = true;
                link.Parameters.Add("role_id", OracleDbType.Int32).Value = roleId;
                link.Parameters.Add("module_key", OracleDbType.Varchar2).Value = module;
                await link.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async Task EnsureProfilePhotoSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            if (string.IsNullOrWhiteSpace(connStr)) return;

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            if (!await SequenceExistsAsync(conn, "PATIENT_PROFILE_PHOTO_SEQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE SEQUENCE PATIENT_PROFILE_PHOTO_SEQ
                        START WITH 1
                        INCREMENT BY 1
                        NOCACHE
                        NOCYCLE", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "PATIENT_PROFILE_PHOTO", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE PATIENT_PROFILE_PHOTO (
                        PHOTO_ID        NUMBER         NOT NULL,
                        MR_NO           VARCHAR2(20)   NOT NULL,
                        IMAGE_PATH      VARCHAR2(500)  NOT NULL,
                        CONTENT_TYPE    VARCHAR2(100),
                        FILE_SIZE       NUMBER,
                        CREATED_AT      DATE           DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT      DATE           DEFAULT SYSDATE NOT NULL,
                        IS_ACTIVE       CHAR(1)        DEFAULT 'Y' NOT NULL,
                        CONSTRAINT PK_PATIENT_PROFILE_PHOTO PRIMARY KEY (PHOTO_ID),
                        CONSTRAINT CHK_PAT_PROF_PHOTO_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
                    )", cancellationToken);

                await ExecuteDdlAsync(conn, @"
                    CREATE OR REPLACE TRIGGER TRG_PATIENT_PROFILE_PHOTO_BI
                    BEFORE INSERT ON PATIENT_PROFILE_PHOTO
                    FOR EACH ROW
                    BEGIN
                        IF :NEW.PHOTO_ID IS NULL THEN
                            SELECT PATIENT_PROFILE_PHOTO_SEQ.NEXTVAL
                              INTO :NEW.PHOTO_ID
                              FROM DUAL;
                        END IF;
                    END;", cancellationToken);

                try
                {
                    await ExecuteDdlAsync(conn, @"
                        CREATE UNIQUE INDEX PATIENT_PROFILE_PHOTO_MR_UK
                            ON PATIENT_PROFILE_PHOTO (MR_NO)", cancellationToken);
                }
                catch (OracleException ex) when (ex.Number == 955) { }
            }
        }

        public async Task EnsureSupportContentSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            if (string.IsNullOrWhiteSpace(connStr)) return;

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(conn, "MOBILE_SUPPORT_CONTACT", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_SUPPORT_CONTACT (
                        CONTACT_ID      NUMBER         DEFAULT 1 NOT NULL,
                        HOSPITAL_NAME   VARCHAR2(200)  NOT NULL,
                        PHONE           VARCHAR2(50)   NOT NULL,
                        EMAIL           VARCHAR2(120)  NOT NULL,
                        ADDRESS         VARCHAR2(400)  NOT NULL,
                        WORKING_HOURS   VARCHAR2(200)  NOT NULL,
                        UPDATED_AT      DATE           DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_SUPPORT_CONTACT PRIMARY KEY (CONTACT_ID)
                    )", cancellationToken);
            }

            await using (var seed = new OracleCommand(@"
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
                  )", conn))
            {
                await seed.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!await SequenceExistsAsync(conn, "MOBILE_FAQ_SEQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE SEQUENCE MOBILE_FAQ_SEQ
                        START WITH 1
                        INCREMENT BY 1
                        NOCACHE
                        NOCYCLE", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_FAQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_FAQ (
                        FAQ_ID        NUMBER         NOT NULL,
                        CATEGORY      VARCHAR2(80)   DEFAULT 'General' NOT NULL,
                        QUESTION_EN   VARCHAR2(500)  NOT NULL,
                        ANSWER_EN     VARCHAR2(2000) NOT NULL,
                        QUESTION_UR   VARCHAR2(500),
                        ANSWER_UR     VARCHAR2(2000),
                        SORT_ORDER    NUMBER         DEFAULT 0 NOT NULL,
                        IS_ACTIVE     CHAR(1)        DEFAULT 'Y' NOT NULL,
                        CREATED_AT    DATE           DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT    DATE           DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_FAQ PRIMARY KEY (FAQ_ID),
                        CONSTRAINT CHK_MOBILE_FAQ_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
                    )", cancellationToken);

                await ExecuteDdlAsync(conn, @"
                    CREATE OR REPLACE TRIGGER TRG_MOBILE_FAQ_BI
                    BEFORE INSERT ON MOBILE_FAQ
                    FOR EACH ROW
                    BEGIN
                        IF :NEW.FAQ_ID IS NULL THEN
                            SELECT MOBILE_FAQ_SEQ.NEXTVAL INTO :NEW.FAQ_ID FROM DUAL;
                        END IF;
                    END;", cancellationToken);
            }

            await EnsureSupportContentColumnsAsync(conn, cancellationToken);
        }

        private static async Task EnsureSupportContentColumnsAsync(
            OracleConnection conn,
            CancellationToken cancellationToken)
        {
            if (await TableExistsAsync(conn, "MOBILE_SUPPORT_CONTACT", cancellationToken))
            {
                await EnsureColumnAsync(
                    conn,
                    "MOBILE_SUPPORT_CONTACT",
                    "UPDATED_AT",
                    "ALTER TABLE MOBILE_SUPPORT_CONTACT ADD (UPDATED_AT DATE DEFAULT SYSDATE NOT NULL)",
                    cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_FAQ", cancellationToken))
            {
                return;
            }

            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "CATEGORY",
                "ALTER TABLE MOBILE_FAQ ADD (CATEGORY VARCHAR2(80) DEFAULT 'General' NOT NULL)",
                cancellationToken);
            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "QUESTION_UR",
                "ALTER TABLE MOBILE_FAQ ADD (QUESTION_UR VARCHAR2(500))",
                cancellationToken);
            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "ANSWER_UR",
                "ALTER TABLE MOBILE_FAQ ADD (ANSWER_UR VARCHAR2(2000))",
                cancellationToken);
            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "SORT_ORDER",
                "ALTER TABLE MOBILE_FAQ ADD (SORT_ORDER NUMBER DEFAULT 0 NOT NULL)",
                cancellationToken);
            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "IS_ACTIVE",
                "ALTER TABLE MOBILE_FAQ ADD (IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL)",
                cancellationToken);
            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "CREATED_AT",
                "ALTER TABLE MOBILE_FAQ ADD (CREATED_AT DATE DEFAULT SYSDATE NOT NULL)",
                cancellationToken);
            await EnsureColumnAsync(
                conn,
                "MOBILE_FAQ",
                "UPDATED_AT",
                "ALTER TABLE MOBILE_FAQ ADD (UPDATED_AT DATE DEFAULT SYSDATE NOT NULL)",
                cancellationToken);
        }

        private static async Task EnsureColumnAsync(
            OracleConnection conn,
            string tableName,
            string columnName,
            string alterSql,
            CancellationToken cancellationToken)
        {
            if (await ColumnExistsAsync(conn, tableName, columnName, cancellationToken))
            {
                return;
            }

            await ExecuteDdlAsync(conn, alterSql, cancellationToken);
        }

        private static async Task<bool> ColumnExistsAsync(
            OracleConnection conn,
            string tableName,
            string columnName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM USER_TAB_COLUMNS
                WHERE TABLE_NAME = :table_name
                  AND COLUMN_NAME = :column_name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("table_name", tableName.ToUpperInvariant()));
            cmd.Parameters.Add(new OracleParameter("column_name", columnName.ToUpperInvariant()));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static async Task EnsurePromotionSettingsAsync(
            OracleConnection conn,
            CancellationToken cancellationToken)
        {
            if (!await TableExistsAsync(conn, "MOBILE_PROMOTION_SETTINGS", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE TABLE MOBILE_PROMOTION_SETTINGS (
                        SETTINGS_ID    NUMBER        DEFAULT 1 NOT NULL,
                        DISPLAY_LIMIT  NUMBER        DEFAULT 5 NOT NULL,
                        UPDATED_AT     DATE          DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_PROMO_SETTINGS PRIMARY KEY (SETTINGS_ID),
                        CONSTRAINT CHK_MOBILE_PROMO_LIMIT CHECK (DISPLAY_LIMIT BETWEEN 1 AND 50)
                    )", cancellationToken);
            }

            await using var cmd = new OracleCommand(@"
                MERGE INTO MOBILE_PROMOTION_SETTINGS t
                USING (SELECT 1 AS SETTINGS_ID FROM DUAL) s
                   ON (t.SETTINGS_ID = s.SETTINGS_ID)
                WHEN NOT MATCHED THEN
                  INSERT (SETTINGS_ID, DISPLAY_LIMIT, UPDATED_AT)
                  VALUES (1, 5, SYSDATE)", conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<bool> TableExistsAsync(
            OracleConnection conn,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM USER_TABLES
                WHERE TABLE_NAME = :table_name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("table_name", tableName));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static async Task<bool> SequenceExistsAsync(
            OracleConnection conn,
            string sequenceName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM USER_SEQUENCES
                WHERE SEQUENCE_NAME = :sequence_name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("sequence_name", sequenceName));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static async Task ExecuteDdlAsync(
            OracleConnection conn,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<ConnectedDatabaseInfo> ProbeConnectionAsync(
            string connectionName,
            string purpose)
        {
            var raw = _configuration.GetConnectionString(connectionName);
            var parsed = ParseOracleConnection(raw);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new ConnectedDatabaseInfo
                {
                    Name = connectionName,
                    Purpose = purpose,
                    Configured = false,
                    Connected = false,
                    Status = "not_configured",
                    Error = $"ConnectionStrings:{connectionName} is empty.",
                };
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await using var conn = new OracleConnection(raw);
                await conn.OpenAsync();

                string? sessionUser = null;
                string? currentSchema = null;
                string? dbName = null;
                string? instanceName = null;
                string? serverHost = null;
                string? serverServiceName = null;
                string? serverTime = null;
                string? banner = null;

                await using (var cmd = new OracleCommand(@"
                    SELECT
                        SYS_CONTEXT('USERENV', 'SESSION_USER') AS SESSION_USER,
                        SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AS CURRENT_SCHEMA,
                        SYS_CONTEXT('USERENV', 'DB_NAME') AS DB_NAME,
                        SYS_CONTEXT('USERENV', 'INSTANCE_NAME') AS INSTANCE_NAME,
                        SYS_CONTEXT('USERENV', 'SERVER_HOST') AS SERVER_HOST,
                        SYS_CONTEXT('USERENV', 'SERVICE_NAME') AS SERVICE_NAME,
                        TO_CHAR(SYSTIMESTAMP, 'YYYY-MM-DD""T""HH24:MI:SS.FF3TZH:TZM') AS SERVER_TIME
                    FROM DUAL", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        sessionUser = reader["SESSION_USER"]?.ToString();
                        currentSchema = reader["CURRENT_SCHEMA"]?.ToString();
                        dbName = reader["DB_NAME"]?.ToString();
                        instanceName = reader["INSTANCE_NAME"]?.ToString();
                        serverHost = reader["SERVER_HOST"]?.ToString();
                        serverServiceName = reader["SERVICE_NAME"]?.ToString();
                        serverTime = reader["SERVER_TIME"]?.ToString();
                    }
                }

                try
                {
                    await using var bannerCmd = new OracleCommand(@"
                        SELECT BANNER
                        FROM V$VERSION
                        WHERE BANNER LIKE 'Oracle%'
                          AND ROWNUM = 1", conn);
                    banner = (await bannerCmd.ExecuteScalarAsync())?.ToString();
                }
                catch
                {
                    // V$VERSION may be restricted; ignore.
                }

                sw.Stop();
                return new ConnectedDatabaseInfo
                {
                    Name = connectionName,
                    Purpose = purpose,
                    Configured = true,
                    Connected = true,
                    Status = "connected",
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    UserId = parsed.UserId,
                    Host = parsed.Host,
                    Port = parsed.Port,
                    ServiceName = parsed.ServiceName,
                    DataSource = parsed.DataSourceSummary,
                    SessionUser = sessionUser,
                    CurrentSchema = currentSchema,
                    DbName = dbName,
                    InstanceName = instanceName,
                    ServerHost = serverHost,
                    ServerServiceName = serverServiceName,
                    ServerTime = serverTime,
                    OracleBanner = banner,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ConnectedDatabaseInfo
                {
                    Name = connectionName,
                    Purpose = purpose,
                    Configured = true,
                    Connected = false,
                    Status = "unreachable",
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    Error = ex.Message,
                    UserId = parsed.UserId,
                    Host = parsed.Host,
                    Port = parsed.Port,
                    ServiceName = parsed.ServiceName,
                    DataSource = parsed.DataSourceSummary,
                };
            }
        }

        private static (
            string? UserId,
            string? Host,
            int? Port,
            string? ServiceName,
            string? DataSourceSummary
        ) ParseOracleConnection(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return (null, null, null, null, null);
            }

            string? userId = null;
            string? dataSource = null;

            try
            {
                var builder = new OracleConnectionStringBuilder(connectionString);
                userId = string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID;
                dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? null : builder.DataSource;
            }
            catch
            {
                // Fall through to regex parsing.
            }

            var source = dataSource ?? connectionString;
            var host = MatchFirst(source, @"\(HOST\s*=\s*([^)\s]+)\)");
            var portRaw = MatchFirst(source, @"\(PORT\s*=\s*([^)\s]+)\)");
            var serviceName =
                MatchFirst(source, @"\(SERVICE_NAME\s*=\s*([^)\s]+)\)") ??
                MatchFirst(source, @"\(SID\s*=\s*([^)\s]+)\)");

            int? port = null;
            if (int.TryParse(portRaw, out var parsedPort))
            {
                port = parsedPort;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = MatchFirst(connectionString, @"User\s*Id\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
            }

            string? summary = null;
            if (!string.IsNullOrWhiteSpace(host) || !string.IsNullOrWhiteSpace(serviceName))
            {
                summary = $"{host ?? "?"}:{port?.ToString() ?? "?"}/{serviceName ?? "?"}";
            }
            else if (!string.IsNullOrWhiteSpace(dataSource) && dataSource.Length <= 120)
            {
                summary = dataSource;
            }

            return (userId?.Trim(), host, port, serviceName, summary);
        }

        private static string? MatchFirst(
            string input,
            string pattern,
            RegexOptions options = RegexOptions.IgnoreCase)
        {
            var match = Regex.Match(input, pattern, options);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}
