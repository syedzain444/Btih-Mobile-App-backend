using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IAppointmentPrepRepository
    {
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
        Task<int> UpsertPendingAlertAsync(AppointmentPrepAlert alert);
        Task<List<AppointmentPrepAlert>> GetDuePendingAlertsAsync(DateTime asOf, int take = 100);
        Task MarkSentAsync(int alertId);
        Task MarkFailedAsync(int alertId, string error);
        Task CancelByAppointmentIdAsync(string appointmentId);
        Task<bool> HasPendingOrSentAsync(string appointmentId, string prepKind);
        Task<List<PrepEligibleAppointment>> GetEligibleAppointmentsAsync(int lookaheadDays);
        Task<PrepEligibleAppointment?> GetAppointmentContextAsync(string appointmentId);
        Task<PrepEligibleAppointment?> GetLatestAppointmentForMrNoAsync(string mrNo, int doctorId, int departmentId);
        Task<string?> GetSpecializationNameAsync(int doctorId);
    }

    public class AppointmentPrepRepository : IAppointmentPrepRepository
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppointmentPrepRepository> _logger;

        public AppointmentPrepRepository(
            IConfiguration configuration,
            ILogger<AppointmentPrepRepository> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string HmisCs => _configuration.GetConnectionString("HMISConnection")
            ?? throw new InvalidOperationException("HMISConnection is not configured.");

        private string HosCs => _configuration.GetConnectionString("HOS_WEB_MVC_LIVE")
            ?? throw new InvalidOperationException("HOS_WEB_MVC_LIVE is not configured.");

        public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        {
            await using var conn = new OracleConnection(HmisCs);
            await conn.OpenAsync(cancellationToken);

            if (!await SequenceExistsAsync(conn, "MOBILE_APPT_PREP_ALERT_SEQ", cancellationToken))
            {
                await ExecAsync(conn, @"
                    CREATE SEQUENCE MOBILE_APPT_PREP_ALERT_SEQ
                      START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE", cancellationToken);
            }

            if (!await TableExistsAsync(conn, "MOBILE_APPOINTMENT_PREP_ALERT", cancellationToken))
            {
                await ExecAsync(conn, @"
                    CREATE TABLE MOBILE_APPOINTMENT_PREP_ALERT (
                        ALERT_ID          NUMBER(10)      NOT NULL,
                        APPOINTMENT_ID    VARCHAR2(64)    NOT NULL,
                        MR_NO             VARCHAR2(32)    NOT NULL,
                        PREP_KIND         VARCHAR2(32)    NOT NULL,
                        DEPARTMENT_HINT   VARCHAR2(120),
                        DOCTOR_NAME       VARCHAR2(120),
                        APPOINTMENT_AT    DATE            NOT NULL,
                        SEND_AT           DATE            NOT NULL,
                        TITLE             VARCHAR2(200)   NOT NULL,
                        BODY              VARCHAR2(1000)  NOT NULL,
                        STATUS            VARCHAR2(20)    DEFAULT 'PENDING' NOT NULL,
                        SENT_AT           DATE,
                        ERROR_MESSAGE     VARCHAR2(500),
                        CREATED_AT        DATE            DEFAULT SYSDATE NOT NULL,
                        UPDATED_AT        DATE            DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_APPT_PREP_ALERT PRIMARY KEY (ALERT_ID),
                        CONSTRAINT UK_MOBILE_APPT_PREP UNIQUE (APPOINTMENT_ID, PREP_KIND),
                        CONSTRAINT CHK_MOBILE_APPT_PREP_KIND CHECK (PREP_KIND IN ('RADIOLOGY', 'GASTRO')),
                        CONSTRAINT CHK_MOBILE_APPT_PREP_STATUS CHECK (STATUS IN ('PENDING', 'SENT', 'CANCELLED', 'FAILED'))
                    )", cancellationToken);

                try
                {
                    await ExecAsync(conn, @"
                        CREATE OR REPLACE TRIGGER TRG_MOBILE_APPT_PREP_BI
                        BEFORE INSERT ON MOBILE_APPOINTMENT_PREP_ALERT
                        FOR EACH ROW
                        BEGIN
                          IF :NEW.ALERT_ID IS NULL THEN
                            SELECT MOBILE_APPT_PREP_ALERT_SEQ.NEXTVAL INTO :NEW.ALERT_ID FROM dual;
                          END IF;
                        END;", cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not create TRG_MOBILE_APPT_PREP_BI (sequence default still works).");
                }

                try
                {
                    await ExecAsync(conn, @"
                        CREATE INDEX IDX_MOBILE_APPT_PREP_DUE
                          ON MOBILE_APPOINTMENT_PREP_ALERT (STATUS, SEND_AT)", cancellationToken);
                }
                catch { /* index may already exist */ }

                try
                {
                    await ExecAsync(conn, @"
                        CREATE INDEX IDX_MOBILE_APPT_PREP_MR
                          ON MOBILE_APPOINTMENT_PREP_ALERT (MR_NO, STATUS)", cancellationToken);
                }
                catch { /* index may already exist */ }
            }
        }

        public async Task<int> UpsertPendingAlertAsync(AppointmentPrepAlert alert)
        {
            await using var conn = new OracleConnection(HmisCs);
            await conn.OpenAsync();

            await using var merge = new OracleCommand(@"
                MERGE INTO MOBILE_APPOINTMENT_PREP_ALERT t
                USING (
                    SELECT :appointment_id AS APPOINTMENT_ID, :prep_kind AS PREP_KIND FROM dual
                ) s
                ON (t.APPOINTMENT_ID = s.APPOINTMENT_ID AND t.PREP_KIND = s.PREP_KIND)
                WHEN MATCHED THEN UPDATE SET
                    MR_NO = :mr_no,
                    DEPARTMENT_HINT = :department_hint,
                    DOCTOR_NAME = :doctor_name,
                    APPOINTMENT_AT = :appointment_at,
                    SEND_AT = :send_at,
                    TITLE = :title,
                    BODY = :body,
                    STATUS = CASE WHEN t.STATUS = 'SENT' THEN t.STATUS ELSE 'PENDING' END,
                    ERROR_MESSAGE = NULL,
                    UPDATED_AT = SYSDATE
                WHERE t.STATUS IN ('PENDING', 'FAILED', 'CANCELLED')
                WHEN NOT MATCHED THEN INSERT (
                    ALERT_ID, APPOINTMENT_ID, MR_NO, PREP_KIND, DEPARTMENT_HINT, DOCTOR_NAME,
                    APPOINTMENT_AT, SEND_AT, TITLE, BODY, STATUS, CREATED_AT, UPDATED_AT
                ) VALUES (
                    MOBILE_APPT_PREP_ALERT_SEQ.NEXTVAL, :appointment_id, :mr_no, :prep_kind,
                    :department_hint, :doctor_name, :appointment_at, :send_at, :title, :body,
                    'PENDING', SYSDATE, SYSDATE
                )", conn);

            merge.BindByName = true;
            merge.Parameters.Add("appointment_id", alert.AppointmentId);
            merge.Parameters.Add("prep_kind", alert.PrepKind);
            merge.Parameters.Add("mr_no", alert.MrNo);
            merge.Parameters.Add("department_hint", (object?)alert.DepartmentHint ?? DBNull.Value);
            merge.Parameters.Add("doctor_name", (object?)alert.DoctorName ?? DBNull.Value);
            merge.Parameters.Add("appointment_at", alert.AppointmentAt);
            merge.Parameters.Add("send_at", alert.SendAt);
            merge.Parameters.Add("title", Truncate(alert.Title, 200));
            merge.Parameters.Add("body", Truncate(alert.Body, 1000));
            await merge.ExecuteNonQueryAsync();

            await using var idCmd = new OracleCommand(@"
                SELECT ALERT_ID FROM MOBILE_APPOINTMENT_PREP_ALERT
                 WHERE APPOINTMENT_ID = :appointment_id AND PREP_KIND = :prep_kind", conn);
            idCmd.BindByName = true;
            idCmd.Parameters.Add("appointment_id", alert.AppointmentId);
            idCmd.Parameters.Add("prep_kind", alert.PrepKind);
            var idObj = await idCmd.ExecuteScalarAsync();
            return idObj == null || idObj == DBNull.Value ? 0 : Convert.ToInt32(idObj);
        }

        public async Task<List<AppointmentPrepAlert>> GetDuePendingAlertsAsync(DateTime asOf, int take = 100)
        {
            var list = new List<AppointmentPrepAlert>();
            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                SELECT * FROM (
                    SELECT ALERT_ID, APPOINTMENT_ID, MR_NO, PREP_KIND, DEPARTMENT_HINT, DOCTOR_NAME,
                           APPOINTMENT_AT, SEND_AT, TITLE, BODY, STATUS, SENT_AT, ERROR_MESSAGE,
                           CREATED_AT, UPDATED_AT
                      FROM MOBILE_APPOINTMENT_PREP_ALERT
                     WHERE STATUS = 'PENDING'
                       AND SEND_AT <= :as_of
                     ORDER BY SEND_AT
                ) WHERE ROWNUM <= :take", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("as_of", asOf);
            cmd.Parameters.Add("take", Math.Clamp(take, 1, 500));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapAlert(reader));
            }
            return list;
        }

        public async Task MarkSentAsync(int alertId)
        {
            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_APPOINTMENT_PREP_ALERT
                   SET STATUS = 'SENT', SENT_AT = SYSDATE, ERROR_MESSAGE = NULL, UPDATED_AT = SYSDATE
                 WHERE ALERT_ID = :alert_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("alert_id", alertId);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkFailedAsync(int alertId, string error)
        {
            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_APPOINTMENT_PREP_ALERT
                   SET STATUS = 'FAILED', ERROR_MESSAGE = :error_message, UPDATED_AT = SYSDATE
                 WHERE ALERT_ID = :alert_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("alert_id", alertId);
            cmd.Parameters.Add("error_message", Truncate(error, 500));
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CancelByAppointmentIdAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId)) return;

            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_APPOINTMENT_PREP_ALERT
                   SET STATUS = 'CANCELLED', UPDATED_AT = SYSDATE
                 WHERE APPOINTMENT_ID = :appointment_id
                   AND STATUS = 'PENDING'", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId.Trim());
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> HasPendingOrSentAsync(string appointmentId, string prepKind)
        {
            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(1) FROM MOBILE_APPOINTMENT_PREP_ALERT
                 WHERE APPOINTMENT_ID = :appointment_id
                   AND PREP_KIND = :prep_kind
                   AND STATUS IN ('PENDING', 'SENT')", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId);
            cmd.Parameters.Add("prep_kind", prepKind);
            await conn.OpenAsync();
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<List<PrepEligibleAppointment>> GetEligibleAppointmentsAsync(int lookaheadDays)
        {
            var list = new List<PrepEligibleAppointment>();
            var days = Math.Clamp(lookaheadDays, 1, 60);

            await using var conn = new OracleConnection(HosCs);
            await using var cmd = new OracleCommand(@"
                SELECT a.APPOINTMENT_ID, a.MRNUM, a.DOCTOR_ID, a.DEPARTMENT_ID, a.PURPOSE,
                       a.APPOINTMENTTIME, a.STATUS, a.CREATED_AT, a.ENTRY_DATE,
                       d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                  FROM APPOINTMENT a
                  JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                  LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                 WHERE NVL(a.IS_ACTIVE, 'Y') = 'Y'
                   AND a.MRNUM IS NOT NULL
                   AND UPPER(NVL(a.STATUS, 'PENDING')) IN ('PENDING', 'CONFIRMED')
                   AND a.CREATED_AT >= SYSDATE - :lookback
                   AND a.CREATED_AT <= SYSDATE + :lookahead
                 ORDER BY a.CREATED_AT DESC", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("lookback", days);
            cmd.Parameters.Add("lookahead", days);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapEligible(reader));
            }
            return list;
        }

        public async Task<PrepEligibleAppointment?> GetAppointmentContextAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId)) return null;

            await using var conn = new OracleConnection(HosCs);
            await using var cmd = new OracleCommand(@"
                SELECT a.APPOINTMENT_ID, a.MRNUM, a.DOCTOR_ID, a.DEPARTMENT_ID, a.PURPOSE,
                       a.APPOINTMENTTIME, a.STATUS, a.CREATED_AT, a.ENTRY_DATE,
                       d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                  FROM APPOINTMENT a
                  JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                  LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                 WHERE a.APPOINTMENT_ID = :appointment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapEligible(reader) : null;
        }

        public async Task<PrepEligibleAppointment?> GetLatestAppointmentForMrNoAsync(
            string mrNo,
            int doctorId,
            int departmentId)
        {
            if (string.IsNullOrWhiteSpace(mrNo)) return null;

            await using var conn = new OracleConnection(HosCs);
            await using var cmd = new OracleCommand(@"
                SELECT * FROM (
                    SELECT a.APPOINTMENT_ID, a.MRNUM, a.DOCTOR_ID, a.DEPARTMENT_ID, a.PURPOSE,
                           a.APPOINTMENTTIME, a.STATUS, a.CREATED_AT, a.ENTRY_DATE,
                           d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                      FROM APPOINTMENT a
                      JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                      LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                     WHERE a.MRNUM = :mr_no
                       AND a.DOCTOR_ID = :doctor_id
                       AND a.DEPARTMENT_ID = :department_id
                     ORDER BY a.CREATED_AT DESC
                ) WHERE ROWNUM = 1", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", mrNo.Trim());
            cmd.Parameters.Add("doctor_id", doctorId);
            cmd.Parameters.Add("department_id", departmentId);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapEligible(reader) : null;
        }

        public async Task<string?> GetSpecializationNameAsync(int doctorId)
        {
            await using var conn = new OracleConnection(HosCs);
            await using var cmd = new OracleCommand(@"
                SELECT s.SPECIALIZATIONNAME
                  FROM DOCTOR d
                  LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                 WHERE d.DOCTOR_ID = :doctor_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("doctor_id", doctorId);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        private static PrepEligibleAppointment MapEligible(OracleDataReader reader) => new()
        {
            AppointmentId = reader["APPOINTMENT_ID"]?.ToString() ?? string.Empty,
            MrNo = reader["MRNUM"]?.ToString(),
            DoctorId = reader["DOCTOR_ID"] != DBNull.Value ? Convert.ToInt32(reader["DOCTOR_ID"]) : 0,
            DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["DEPARTMENT_ID"]) : 0,
            Purpose = reader["PURPOSE"]?.ToString(),
            AppointmentTimeLabel = reader["APPOINTMENTTIME"]?.ToString(),
            Status = reader["STATUS"]?.ToString(),
            CreatedAt = reader["CREATED_AT"] != DBNull.Value ? Convert.ToDateTime(reader["CREATED_AT"]) : null,
            EntryDate = reader["ENTRY_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["ENTRY_DATE"]) : null,
            DoctorName = reader["DOCTOR_NAME"]?.ToString(),
            SpecializationName = reader["SPECIALIZATIONNAME"]?.ToString(),
        };

        private static AppointmentPrepAlert MapAlert(OracleDataReader reader) => new()
        {
            AlertId = Convert.ToInt32(reader["ALERT_ID"]),
            AppointmentId = reader["APPOINTMENT_ID"]?.ToString() ?? string.Empty,
            MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
            PrepKind = reader["PREP_KIND"]?.ToString() ?? string.Empty,
            DepartmentHint = reader["DEPARTMENT_HINT"]?.ToString(),
            DoctorName = reader["DOCTOR_NAME"]?.ToString(),
            AppointmentAt = Convert.ToDateTime(reader["APPOINTMENT_AT"]),
            SendAt = Convert.ToDateTime(reader["SEND_AT"]),
            Title = reader["TITLE"]?.ToString() ?? string.Empty,
            Body = reader["BODY"]?.ToString() ?? string.Empty,
            Status = reader["STATUS"]?.ToString() ?? AppointmentPrepAlertStatuses.Pending,
            SentAt = reader["SENT_AT"] != DBNull.Value ? Convert.ToDateTime(reader["SENT_AT"]) : null,
            ErrorMessage = reader["ERROR_MESSAGE"]?.ToString(),
            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
            UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
        };

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);

        private static async Task<bool> TableExistsAsync(
            OracleConnection conn,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(
                "SELECT COUNT(1) FROM USER_TABLES WHERE TABLE_NAME = :name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("name", tableName.ToUpperInvariant());
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }

        private static async Task<bool> SequenceExistsAsync(
            OracleConnection conn,
            string sequenceName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(
                "SELECT COUNT(1) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = :name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("name", sequenceName.ToUpperInvariant());
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }

        private static async Task ExecAsync(
            OracleConnection conn,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
