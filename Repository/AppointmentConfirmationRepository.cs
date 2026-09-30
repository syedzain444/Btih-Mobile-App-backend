using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IAppointmentConfirmationRepository
    {
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
        Task UpsertQrAsync(AppointmentConfirmationQrDto qr);
        Task<AppointmentConfirmationQrDto?> GetByTokenAsync(string qrToken);
        Task<AppointmentConfirmationQrDto?> GetByAppointmentIdAsync(string appointmentId);
        Task<PatientAppointment?> GetAppointmentAsync(string appointmentId);
        Task<AppointmentConfirmationQrDto?> GetLiveAppointmentSnapshotAsync(string appointmentId);
    }

    public class AppointmentConfirmationRepository : IAppointmentConfirmationRepository
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppointmentConfirmationRepository> _logger;

        public AppointmentConfirmationRepository(
            IConfiguration configuration,
            ILogger<AppointmentConfirmationRepository> logger)
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

            if (!await ExistsAsync(conn, "USER_SEQUENCES", "SEQUENCE_NAME", "MOBILE_APPT_CONFIRM_QR_SEQ", cancellationToken))
            {
                await ExecAsync(conn, @"
                    CREATE SEQUENCE MOBILE_APPT_CONFIRM_QR_SEQ
                      START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE", cancellationToken);
            }

            if (!await ExistsAsync(conn, "USER_TABLES", "TABLE_NAME", "MOBILE_APPOINTMENT_CONFIRM_QR", cancellationToken))
            {
                await ExecAsync(conn, @"
                    CREATE TABLE MOBILE_APPOINTMENT_CONFIRM_QR (
                        QR_ID             NUMBER(10)      NOT NULL,
                        APPOINTMENT_ID    VARCHAR2(64)    NOT NULL,
                        QR_TOKEN          VARCHAR2(64)    NOT NULL,
                        MR_NO             VARCHAR2(32),
                        PATIENT_NAME      VARCHAR2(120),
                        PHONE             VARCHAR2(40),
                        DOCTOR_NAME       VARCHAR2(120),
                        DEPARTMENT_ID     NUMBER(10),
                        DEPARTMENT_HINT   VARCHAR2(120),
                        APPOINTMENT_TIME  VARCHAR2(120),
                        PURPOSE           VARCHAR2(500),
                        APPT_STATUS       VARCHAR2(40),
                        QR_PAYLOAD        VARCHAR2(500)   NOT NULL,
                        CREATED_AT        DATE            DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_APPT_CONFIRM_QR PRIMARY KEY (QR_ID),
                        CONSTRAINT UK_MOBILE_APPT_CONFIRM_TOKEN UNIQUE (QR_TOKEN),
                        CONSTRAINT UK_MOBILE_APPT_CONFIRM_APPT UNIQUE (APPOINTMENT_ID)
                    )", cancellationToken);
            }
        }

        public async Task UpsertQrAsync(AppointmentConfirmationQrDto qr)
        {
            await EnsureSchemaAsync();
            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                MERGE INTO MOBILE_APPOINTMENT_CONFIRM_QR t
                USING (SELECT :appointment_id AS APPOINTMENT_ID FROM dual) s
                ON (t.APPOINTMENT_ID = s.APPOINTMENT_ID)
                WHEN MATCHED THEN UPDATE SET
                    QR_TOKEN = :qr_token,
                    MR_NO = :mr_no,
                    PATIENT_NAME = :patient_name,
                    PHONE = :phone,
                    DOCTOR_NAME = :doctor_name,
                    DEPARTMENT_ID = :department_id,
                    DEPARTMENT_HINT = :department_hint,
                    APPOINTMENT_TIME = :appointment_time,
                    PURPOSE = :purpose,
                    APPT_STATUS = :appt_status,
                    QR_PAYLOAD = :qr_payload
                WHEN NOT MATCHED THEN INSERT (
                    QR_ID, APPOINTMENT_ID, QR_TOKEN, MR_NO, PATIENT_NAME, PHONE, DOCTOR_NAME,
                    DEPARTMENT_ID, DEPARTMENT_HINT, APPOINTMENT_TIME, PURPOSE, APPT_STATUS, QR_PAYLOAD, CREATED_AT
                ) VALUES (
                    MOBILE_APPT_CONFIRM_QR_SEQ.NEXTVAL, :appointment_id, :qr_token, :mr_no, :patient_name, :phone, :doctor_name,
                    :department_id, :department_hint, :appointment_time, :purpose, :appt_status, :qr_payload, SYSDATE
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", qr.AppointmentId);
            cmd.Parameters.Add("qr_token", qr.QrToken);
            cmd.Parameters.Add("mr_no", (object?)qr.MrNo ?? DBNull.Value);
            cmd.Parameters.Add("patient_name", Truncate(qr.PatientName, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("phone", Truncate(qr.Phone, 40) ?? (object)DBNull.Value);
            cmd.Parameters.Add("doctor_name", Truncate(qr.DoctorName, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("department_id", qr.DepartmentId.HasValue ? qr.DepartmentId.Value : (object)DBNull.Value);
            cmd.Parameters.Add("department_hint", Truncate(qr.DepartmentHint, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("appointment_time", Truncate(qr.AppointmentTime, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("purpose", Truncate(qr.Purpose, 500) ?? (object)DBNull.Value);
            cmd.Parameters.Add("appt_status", Truncate(qr.Status, 40) ?? (object)DBNull.Value);
            cmd.Parameters.Add("qr_payload", Truncate(qr.QrPayload, 500)!);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<AppointmentConfirmationQrDto?> GetByTokenAsync(string qrToken)
        {
            if (string.IsNullOrWhiteSpace(qrToken)) return null;
            await EnsureSchemaAsync();

            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                SELECT APPOINTMENT_ID, QR_TOKEN, QR_PAYLOAD, MR_NO, PATIENT_NAME, PHONE, DOCTOR_NAME,
                       DEPARTMENT_ID, DEPARTMENT_HINT, APPOINTMENT_TIME, PURPOSE, APPT_STATUS, CREATED_AT
                  FROM MOBILE_APPOINTMENT_CONFIRM_QR
                 WHERE QR_TOKEN = :qr_token", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("qr_token", qrToken.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapQr(reader) : null;
        }

        public async Task<AppointmentConfirmationQrDto?> GetByAppointmentIdAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId)) return null;
            await EnsureSchemaAsync();

            await using var conn = new OracleConnection(HmisCs);
            await using var cmd = new OracleCommand(@"
                SELECT APPOINTMENT_ID, QR_TOKEN, QR_PAYLOAD, MR_NO, PATIENT_NAME, PHONE, DOCTOR_NAME,
                       DEPARTMENT_ID, DEPARTMENT_HINT, APPOINTMENT_TIME, PURPOSE, APPT_STATUS, CREATED_AT
                  FROM MOBILE_APPOINTMENT_CONFIRM_QR
                 WHERE APPOINTMENT_ID = :appointment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapQr(reader) : null;
        }

        public async Task<PatientAppointment?> GetAppointmentAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId)) return null;

            await using var conn = new OracleConnection(HosCs);
            await using var cmd = new OracleCommand(@"
                SELECT a.APPOINTMENT_ID, a.NAME, a.PHONE, a.MRNUM, a.EMAIL, a.WEEK_ID,
                       a.APPOINTMENTTIME, a.STATUS, a.DOCTOR_ID, a.DEPARTMENT_ID, a.PURPOSE, a.CREATED_AT,
                       d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                  FROM APPOINTMENT a
                  JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                  LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                 WHERE a.APPOINTMENT_ID = :appointment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PatientAppointment
            {
                AppointmentId = reader["APPOINTMENT_ID"]?.ToString() ?? string.Empty,
                Name = reader["NAME"]?.ToString() ?? string.Empty,
                PhoneNo = reader["PHONE"]?.ToString() ?? string.Empty,
                MRNo = reader["MRNUM"]?.ToString() ?? string.Empty,
                Email = reader["EMAIL"]?.ToString() ?? string.Empty,
                weekId = reader["WEEK_ID"] != DBNull.Value ? Convert.ToInt32(reader["WEEK_ID"]) : 0,
                AppointmentTime = reader["APPOINTMENTTIME"]?.ToString() ?? string.Empty,
                Status = reader["STATUS"]?.ToString() ?? string.Empty,
                DoctorId = reader["DOCTOR_ID"] != DBNull.Value ? Convert.ToInt32(reader["DOCTOR_ID"]) : 0,
                DoctorName = reader["DOCTOR_NAME"]?.ToString() ?? string.Empty,
                DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["DEPARTMENT_ID"]) : 0,
                purpose = reader["PURPOSE"]?.ToString() ?? string.Empty,
                CreatedAt = reader["CREATED_AT"] != DBNull.Value ? Convert.ToDateTime(reader["CREATED_AT"]) : null,
            };
        }

        public async Task<AppointmentConfirmationQrDto?> GetLiveAppointmentSnapshotAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId)) return null;

            await using var conn = new OracleConnection(HosCs);
            await using var cmd = new OracleCommand(@"
                SELECT a.APPOINTMENT_ID, a.NAME, a.PHONE, a.MRNUM, a.APPOINTMENTTIME, a.STATUS,
                       a.PURPOSE, a.DEPARTMENT_ID, d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                  FROM APPOINTMENT a
                  JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                  LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                 WHERE a.APPOINTMENT_ID = :appointment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new AppointmentConfirmationQrDto
            {
                AppointmentId = reader["APPOINTMENT_ID"]?.ToString() ?? string.Empty,
                MrNo = reader["MRNUM"]?.ToString(),
                PatientName = reader["NAME"]?.ToString(),
                Phone = reader["PHONE"]?.ToString(),
                DoctorName = reader["DOCTOR_NAME"]?.ToString(),
                DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["DEPARTMENT_ID"]) : null,
                DepartmentHint = reader["SPECIALIZATIONNAME"]?.ToString(),
                AppointmentTime = reader["APPOINTMENTTIME"]?.ToString(),
                Purpose = reader["PURPOSE"]?.ToString(),
                Status = reader["STATUS"]?.ToString(),
                Verified = true,
            };
        }

        private static AppointmentConfirmationQrDto MapQr(OracleDataReader reader) => new()
        {
            AppointmentId = reader["APPOINTMENT_ID"]?.ToString() ?? string.Empty,
            QrToken = reader["QR_TOKEN"]?.ToString() ?? string.Empty,
            QrPayload = reader["QR_PAYLOAD"]?.ToString() ?? string.Empty,
            MrNo = reader["MR_NO"]?.ToString(),
            PatientName = reader["PATIENT_NAME"]?.ToString(),
            Phone = reader["PHONE"]?.ToString(),
            DoctorName = reader["DOCTOR_NAME"]?.ToString(),
            DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["DEPARTMENT_ID"]) : null,
            DepartmentHint = reader["DEPARTMENT_HINT"]?.ToString(),
            AppointmentTime = reader["APPOINTMENT_TIME"]?.ToString(),
            Purpose = reader["PURPOSE"]?.ToString(),
            Status = reader["APPT_STATUS"]?.ToString(),
            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
            Verified = true,
        };

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);

        private static async Task<bool> ExistsAsync(
            OracleConnection conn,
            string view,
            string column,
            string name,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(
                $"SELECT COUNT(1) FROM {view} WHERE {column} = :name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("name", name.ToUpperInvariant());
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static async Task ExecAsync(OracleConnection conn, string sql, CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
