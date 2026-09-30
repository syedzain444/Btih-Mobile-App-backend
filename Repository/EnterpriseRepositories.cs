using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IPaymentRepository
    {
        Task EnsureQrSchemaAsync(CancellationToken cancellationToken = default);
        Task<int> CreateIntentAsync(PaymentIntentDto intent);
        Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null);
        Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo);
        Task<bool> UpdateStatusAsync(int paymentId, string status, string? gatewayRef, string? failureReason);
        Task AddTransactionAsync(int paymentId, string eventType, string? gatewayRef, string? rawPayload);
        Task SavePaymentQrAsync(
            int paymentId,
            string qrToken,
            string qrPayload,
            PaymentAppointmentDetailsDto? appointment);
        Task<PaymentQrResolveDto?> GetPaymentQrByTokenAsync(string qrToken);
        Task AttachQrToIntentAsync(PaymentIntentDto intent);
        Task<PaymentAppointmentDetailsDto?> GetAppointmentDetailsAsync(string appointmentId);
        Task<PaymentAppointmentDetailsDto?> GetLatestActiveAppointmentAsync(string mrNo);
    }

    public class PaymentRepository : IPaymentRepository
    {
        private readonly IConfiguration _configuration;

        public PaymentRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task<int> CreateIntentAsync(PaymentIntentDto intent)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_PAYMENT_INTENT (
                    MR_NO, BILL_ID, INVOICE_NO, AMOUNT, CURRENCY, STATUS, GATEWAY, CHECKOUT_URL, RETURN_URL, GATEWAY_REF
                ) VALUES (
                    :mr_no, :bill_id, :invoice_no, :amount, :currency, :status, :gateway, :checkout_url, :return_url, :gateway_ref
                ) RETURNING PAYMENT_ID INTO :payment_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", intent.MrNo));
            cmd.Parameters.Add(new OracleParameter("bill_id", intent.BillId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("invoice_no", intent.InvoiceNo ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("amount", intent.Amount));
            cmd.Parameters.Add(new OracleParameter("currency", intent.Currency));
            cmd.Parameters.Add(new OracleParameter("status", intent.Status));
            cmd.Parameters.Add(new OracleParameter("gateway", intent.Gateway));
            cmd.Parameters.Add(new OracleParameter("checkout_url", intent.CheckoutUrl ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("return_url", intent.ReturnUrl ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("gateway_ref", intent.GatewayRef ?? (object)DBNull.Value));

            var outParam = new OracleParameter("payment_id", OracleDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(outParam.Value.ToString());
        }

        public async Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            var sql = @"
                SELECT PAYMENT_ID, MR_NO, BILL_ID, INVOICE_NO, AMOUNT, CURRENCY, STATUS, GATEWAY,
                       CHECKOUT_URL, GATEWAY_REF, CREATED_AT, PAID_AT
                FROM MOBILE_PAYMENT_INTENT
                WHERE PAYMENT_ID = :payment_id";
            if (!string.IsNullOrWhiteSpace(mrNo))
            {
                sql += " AND MR_NO = :mr_no";
            }

            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("payment_id", paymentId));
            if (!string.IsNullOrWhiteSpace(mrNo))
            {
                cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            }

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapPayment(reader) : null;
        }

        public async Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo)
        {
            var list = new List<PaymentIntentDto>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT PAYMENT_ID, MR_NO, BILL_ID, INVOICE_NO, AMOUNT, CURRENCY, STATUS, GATEWAY,
                       CHECKOUT_URL, GATEWAY_REF, CREATED_AT, PAID_AT
                FROM MOBILE_PAYMENT_INTENT
                WHERE MR_NO = :mr_no
                ORDER BY CREATED_AT DESC", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapPayment(reader));
            }
            return list;
        }

        public async Task<bool> UpdateStatusAsync(int paymentId, string status, string? gatewayRef, string? failureReason)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_PAYMENT_INTENT
                SET STATUS = :status,
                    GATEWAY_REF = NVL(:gateway_ref, GATEWAY_REF),
                    FAILURE_REASON = :failure_reason,
                    PAID_AT = CASE WHEN :status = 'PAID' THEN SYSDATE ELSE PAID_AT END,
                    UPDATED_AT = SYSDATE
                WHERE PAYMENT_ID = :payment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("gateway_ref", gatewayRef ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("failure_reason", failureReason ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("payment_id", paymentId));
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task AddTransactionAsync(int paymentId, string eventType, string? gatewayRef, string? rawPayload)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_PAYMENT_TRANSACTION (PAYMENT_ID, EVENT_TYPE, GATEWAY_REF, RAW_PAYLOAD)
                VALUES (:payment_id, :event_type, :gateway_ref, :raw_payload)", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("payment_id", paymentId));
            cmd.Parameters.Add(new OracleParameter("event_type", eventType));
            cmd.Parameters.Add(new OracleParameter("gateway_ref", gatewayRef ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("raw_payload", rawPayload ?? (object)DBNull.Value));
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task EnsureQrSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            if (!await ObjectExistsAsync(conn, "USER_SEQUENCES", "MOBILE_PAYMENT_QR_SEQ", cancellationToken))
            {
                await ExecAsync(conn, @"
                    CREATE SEQUENCE MOBILE_PAYMENT_QR_SEQ
                      START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE", cancellationToken);
            }

            if (!await ObjectExistsAsync(conn, "USER_TABLES", "MOBILE_PAYMENT_QR", cancellationToken))
            {
                await ExecAsync(conn, @"
                    CREATE TABLE MOBILE_PAYMENT_QR (
                        QR_ID             NUMBER(10)      NOT NULL,
                        PAYMENT_ID        NUMBER(10)      NOT NULL,
                        QR_TOKEN          VARCHAR2(64)    NOT NULL,
                        APPOINTMENT_ID    VARCHAR2(64),
                        PATIENT_NAME      VARCHAR2(120),
                        DOCTOR_NAME       VARCHAR2(120),
                        DEPARTMENT_ID     NUMBER(10),
                        DEPARTMENT_HINT   VARCHAR2(120),
                        APPOINTMENT_TIME  VARCHAR2(120),
                        PURPOSE           VARCHAR2(500),
                        APPT_STATUS       VARCHAR2(40),
                        QR_PAYLOAD        VARCHAR2(500)   NOT NULL,
                        CREATED_AT        DATE            DEFAULT SYSDATE NOT NULL,
                        CONSTRAINT PK_MOBILE_PAYMENT_QR PRIMARY KEY (QR_ID),
                        CONSTRAINT UK_MOBILE_PAYMENT_QR_TOKEN UNIQUE (QR_TOKEN),
                        CONSTRAINT UK_MOBILE_PAYMENT_QR_PAY UNIQUE (PAYMENT_ID)
                    )", cancellationToken);
            }
        }

        public async Task SavePaymentQrAsync(
            int paymentId,
            string qrToken,
            string qrPayload,
            PaymentAppointmentDetailsDto? appointment)
        {
            await EnsureQrSchemaAsync();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                MERGE INTO MOBILE_PAYMENT_QR t
                USING (SELECT :payment_id AS PAYMENT_ID FROM dual) s
                ON (t.PAYMENT_ID = s.PAYMENT_ID)
                WHEN MATCHED THEN UPDATE SET
                    QR_TOKEN = :qr_token,
                    APPOINTMENT_ID = :appointment_id,
                    PATIENT_NAME = :patient_name,
                    DOCTOR_NAME = :doctor_name,
                    DEPARTMENT_ID = :department_id,
                    DEPARTMENT_HINT = :department_hint,
                    APPOINTMENT_TIME = :appointment_time,
                    PURPOSE = :purpose,
                    APPT_STATUS = :appt_status,
                    QR_PAYLOAD = :qr_payload
                WHEN NOT MATCHED THEN INSERT (
                    QR_ID, PAYMENT_ID, QR_TOKEN, APPOINTMENT_ID, PATIENT_NAME, DOCTOR_NAME,
                    DEPARTMENT_ID, DEPARTMENT_HINT, APPOINTMENT_TIME, PURPOSE, APPT_STATUS, QR_PAYLOAD, CREATED_AT
                ) VALUES (
                    MOBILE_PAYMENT_QR_SEQ.NEXTVAL, :payment_id, :qr_token, :appointment_id, :patient_name, :doctor_name,
                    :department_id, :department_hint, :appointment_time, :purpose, :appt_status, :qr_payload, SYSDATE
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("payment_id", paymentId);
            cmd.Parameters.Add("qr_token", qrToken);
            cmd.Parameters.Add("appointment_id", (object?)appointment?.AppointmentId ?? DBNull.Value);
            cmd.Parameters.Add("patient_name", Truncate(appointment?.PatientName, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("doctor_name", Truncate(appointment?.DoctorName, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("department_id", appointment?.DepartmentId.HasValue == true ? appointment.DepartmentId.Value : (object)DBNull.Value);
            cmd.Parameters.Add("department_hint", Truncate(appointment?.DepartmentHint, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("appointment_time", Truncate(appointment?.AppointmentTime, 120) ?? (object)DBNull.Value);
            cmd.Parameters.Add("purpose", Truncate(appointment?.Purpose, 500) ?? (object)DBNull.Value);
            cmd.Parameters.Add("appt_status", Truncate(appointment?.Status, 40) ?? (object)DBNull.Value);
            cmd.Parameters.Add("qr_payload", Truncate(qrPayload, 500)!);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PaymentQrResolveDto?> GetPaymentQrByTokenAsync(string qrToken)
        {
            if (string.IsNullOrWhiteSpace(qrToken)) return null;
            await EnsureQrSchemaAsync();

            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT q.QR_TOKEN, q.QR_PAYLOAD, q.APPOINTMENT_ID, q.PATIENT_NAME, q.DOCTOR_NAME,
                       q.DEPARTMENT_ID, q.DEPARTMENT_HINT, q.APPOINTMENT_TIME, q.PURPOSE, q.APPT_STATUS,
                       p.PAYMENT_ID, p.MR_NO, p.BILL_ID, p.INVOICE_NO, p.AMOUNT, p.CURRENCY, p.STATUS,
                       p.CHECKOUT_URL, p.GATEWAY_REF, p.CREATED_AT
                  FROM MOBILE_PAYMENT_QR q
                  JOIN MOBILE_PAYMENT_INTENT p ON p.PAYMENT_ID = q.PAYMENT_ID
                 WHERE q.QR_TOKEN = :qr_token", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("qr_token", qrToken.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PaymentQrResolveDto
            {
                PaymentId = Convert.ToInt32(reader["PAYMENT_ID"]),
                QrToken = reader["QR_TOKEN"]?.ToString() ?? string.Empty,
                QrPayload = reader["QR_PAYLOAD"]?.ToString(),
                Amount = Convert.ToDecimal(reader["AMOUNT"]),
                Currency = reader["CURRENCY"]?.ToString() ?? "PKR",
                Status = reader["STATUS"]?.ToString() ?? string.Empty,
                BillId = reader["BILL_ID"]?.ToString(),
                InvoiceNo = reader["INVOICE_NO"]?.ToString(),
                CheckoutUrl = reader["CHECKOUT_URL"]?.ToString(),
                GatewayRef = reader["GATEWAY_REF"]?.ToString(),
                CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                Appointment = string.IsNullOrWhiteSpace(reader["APPOINTMENT_ID"]?.ToString())
                    && string.IsNullOrWhiteSpace(reader["DOCTOR_NAME"]?.ToString())
                    && string.IsNullOrWhiteSpace(reader["APPOINTMENT_TIME"]?.ToString())
                    ? null
                    : new PaymentAppointmentDetailsDto
                    {
                        AppointmentId = reader["APPOINTMENT_ID"]?.ToString(),
                        PatientName = reader["PATIENT_NAME"]?.ToString(),
                        MrNo = reader["MR_NO"]?.ToString(),
                        DoctorName = reader["DOCTOR_NAME"]?.ToString(),
                        DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value
                            ? Convert.ToInt32(reader["DEPARTMENT_ID"])
                            : null,
                        DepartmentHint = reader["DEPARTMENT_HINT"]?.ToString(),
                        AppointmentTime = reader["APPOINTMENT_TIME"]?.ToString(),
                        Purpose = reader["PURPOSE"]?.ToString(),
                        Status = reader["APPT_STATUS"]?.ToString(),
                    },
            };
        }

        public async Task AttachQrToIntentAsync(PaymentIntentDto intent)
        {
            if (intent.PaymentId <= 0) return;
            await EnsureQrSchemaAsync();

            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT QR_TOKEN, QR_PAYLOAD, APPOINTMENT_ID, PATIENT_NAME, DOCTOR_NAME,
                       DEPARTMENT_ID, DEPARTMENT_HINT, APPOINTMENT_TIME, PURPOSE, APPT_STATUS
                  FROM MOBILE_PAYMENT_QR
                 WHERE PAYMENT_ID = :payment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("payment_id", intent.PaymentId);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return;

            intent.QrToken = reader["QR_TOKEN"]?.ToString();
            intent.QrPayload = reader["QR_PAYLOAD"]?.ToString();
            intent.Appointment = new PaymentAppointmentDetailsDto
            {
                AppointmentId = reader["APPOINTMENT_ID"]?.ToString(),
                PatientName = reader["PATIENT_NAME"]?.ToString(),
                MrNo = intent.MrNo,
                DoctorName = reader["DOCTOR_NAME"]?.ToString(),
                DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value
                    ? Convert.ToInt32(reader["DEPARTMENT_ID"])
                    : null,
                DepartmentHint = reader["DEPARTMENT_HINT"]?.ToString(),
                AppointmentTime = reader["APPOINTMENT_TIME"]?.ToString(),
                Purpose = reader["PURPOSE"]?.ToString(),
                Status = reader["APPT_STATUS"]?.ToString(),
            };
        }

        public async Task<PaymentAppointmentDetailsDto?> GetAppointmentDetailsAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId)) return null;

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT a.APPOINTMENT_ID, a.NAME, a.MRNUM, a.APPOINTMENTTIME, a.STATUS, a.PURPOSE,
                       a.DEPARTMENT_ID, d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                  FROM APPOINTMENT a
                  JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                  LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                 WHERE a.APPOINTMENT_ID = :appointment_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("appointment_id", appointmentId.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapAppointmentDetails(reader) : null;
        }

        public async Task<PaymentAppointmentDetailsDto?> GetLatestActiveAppointmentAsync(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo)) return null;

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT * FROM (
                    SELECT a.APPOINTMENT_ID, a.NAME, a.MRNUM, a.APPOINTMENTTIME, a.STATUS, a.PURPOSE,
                           a.DEPARTMENT_ID, d.DOCTOR_NAME, s.SPECIALIZATIONNAME
                      FROM APPOINTMENT a
                      JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                      LEFT JOIN SPECIALIZATION s ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                     WHERE a.MRNUM = :mr_no
                       AND NVL(a.IS_ACTIVE, 'Y') = 'Y'
                       AND UPPER(NVL(a.STATUS, 'PENDING')) IN ('PENDING', 'CONFIRMED')
                     ORDER BY a.CREATED_AT DESC
                ) WHERE ROWNUM = 1", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", mrNo.Trim());
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapAppointmentDetails(reader) : null;
        }

        private static PaymentAppointmentDetailsDto MapAppointmentDetails(OracleDataReader reader) => new()
        {
            AppointmentId = reader["APPOINTMENT_ID"]?.ToString(),
            PatientName = reader["NAME"]?.ToString(),
            MrNo = reader["MRNUM"]?.ToString(),
            DoctorName = reader["DOCTOR_NAME"]?.ToString(),
            DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value
                ? Convert.ToInt32(reader["DEPARTMENT_ID"])
                : null,
            DepartmentHint = reader["SPECIALIZATIONNAME"]?.ToString(),
            AppointmentTime = reader["APPOINTMENTTIME"]?.ToString(),
            Purpose = reader["PURPOSE"]?.ToString(),
            Status = reader["STATUS"]?.ToString(),
        };

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);

        private static async Task<bool> ObjectExistsAsync(
            OracleConnection conn,
            string catalogView,
            string objectName,
            CancellationToken cancellationToken)
        {
            var column = catalogView.Contains("SEQUENCE", StringComparison.OrdinalIgnoreCase)
                ? "SEQUENCE_NAME"
                : "TABLE_NAME";
            await using var cmd = new OracleCommand(
                $"SELECT COUNT(1) FROM {catalogView} WHERE {column} = :name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("name", objectName.ToUpperInvariant());
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

        private static PaymentIntentDto MapPayment(OracleDataReader reader) => new()
        {
            PaymentId = Convert.ToInt32(reader["PAYMENT_ID"]),
            MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
            BillId = reader["BILL_ID"]?.ToString(),
            InvoiceNo = reader["INVOICE_NO"]?.ToString(),
            Amount = Convert.ToDecimal(reader["AMOUNT"]),
            Currency = reader["CURRENCY"]?.ToString() ?? "PKR",
            Status = reader["STATUS"]?.ToString() ?? "PENDING",
            Gateway = reader["GATEWAY"]?.ToString() ?? string.Empty,
            CheckoutUrl = reader["CHECKOUT_URL"]?.ToString(),
            GatewayRef = reader["GATEWAY_REF"]?.ToString(),
            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
            PaidAt = reader["PAID_AT"] != DBNull.Value ? Convert.ToDateTime(reader["PAID_AT"]) : null,
        };
    }

    public interface IAdminRepository
    {
        Task<AdminUserDto?> GetByUsernameAsync(string username);
        Task<string?> GetPasswordHashAsync(string username);
        Task<bool> CreateAdminAsync(AdminUserDto user, string passwordHash);
        Task<bool> UpdatePasswordHashAsync(string username, string passwordHash);
        Task<List<SupportTicketDto>> GetOpenTicketsAsync();
        Task<SupportTicketDto?> GetTicketByIdAsync(int ticketId);
        Task<bool> UpdateTicketAsync(int ticketId, string status, string? adminNotes);
        Task<bool> UpdateRefillAsync(int refillId, string status, string? statusMessage);
        Task<List<RefillRequestItem>> GetPendingRefillsAsync();
    }

    public class AdminRepository : IAdminRepository
    {
        private readonly IConfiguration _configuration;

        public AdminRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task<AdminUserDto?> GetByUsernameAsync(string username)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT ADMIN_ID, USERNAME, DISPLAY_NAME, ROLE, PASSWORD_HASH, IS_ACTIVE
                FROM MOBILE_ADMIN_USER
                WHERE USERNAME = :username AND IS_ACTIVE = 'Y'", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("username", username));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new AdminUserDto
            {
                AdminId = Convert.ToInt32(reader["ADMIN_ID"]),
                Username = reader["USERNAME"]?.ToString() ?? string.Empty,
                DisplayName = reader["DISPLAY_NAME"]?.ToString(),
                Role = reader["ROLE"]?.ToString() ?? "Staff",
                IsActive = (reader["IS_ACTIVE"]?.ToString() ?? "Y") == "Y",
            };
        }

        public async Task<string?> GetPasswordHashAsync(string username)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT PASSWORD_HASH FROM MOBILE_ADMIN_USER WHERE USERNAME = :username", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("username", username));
            await conn.OpenAsync();
            return (await cmd.ExecuteScalarAsync())?.ToString();
        }

        public async Task<bool> CreateAdminAsync(AdminUserDto user, string passwordHash)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_ADMIN_USER (USERNAME, DISPLAY_NAME, PASSWORD_HASH, ROLE)
                VALUES (:username, :display_name, :password_hash, :role)", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("username", user.Username));
            cmd.Parameters.Add(new OracleParameter("display_name", user.DisplayName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("password_hash", passwordHash));
            cmd.Parameters.Add(new OracleParameter("role", user.Role));
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> UpdatePasswordHashAsync(string username, string passwordHash)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_ADMIN_USER SET PASSWORD_HASH = :password_hash, UPDATED_AT = SYSDATE
                WHERE USERNAME = :username", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("password_hash", passwordHash));
            cmd.Parameters.Add(new OracleParameter("username", username));
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<List<SupportTicketDto>> GetOpenTicketsAsync()
        {
            var list = new List<SupportTicketDto>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT TICKET_ID, MR_NO, CONTACT_NAME, CONTACT_PHONE, CONTACT_EMAIL,
                       CATEGORY, SUBJECT, DESCRIPTION, STATUS, ADMIN_NOTES, CREATED_AT, UPDATED_AT
                FROM MOBILE_SUPPORT_TICKET
                WHERE STATUS IN ('OPEN', 'IN_PROGRESS')
                ORDER BY CREATED_AT DESC", conn);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapTicket(reader));
            return list;
        }

        public async Task<SupportTicketDto?> GetTicketByIdAsync(int ticketId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT TICKET_ID, MR_NO, CONTACT_NAME, CONTACT_PHONE, CONTACT_EMAIL,
                       CATEGORY, SUBJECT, DESCRIPTION, STATUS, ADMIN_NOTES, CREATED_AT, UPDATED_AT
                FROM MOBILE_SUPPORT_TICKET
                WHERE TICKET_ID = :ticket_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("ticket_id", ticketId));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapTicket(reader);
        }

        public async Task<bool> UpdateTicketAsync(int ticketId, string status, string? adminNotes)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_SUPPORT_TICKET
                SET STATUS = :status, ADMIN_NOTES = :admin_notes, UPDATED_AT = SYSDATE
                WHERE TICKET_ID = :ticket_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("admin_notes", adminNotes ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("ticket_id", ticketId));
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> UpdateRefillAsync(int refillId, string status, string? statusMessage)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_MED_REFILL_REQUEST
                SET STATUS = :status, STATUS_MESSAGE = :status_message, UPDATED_AT = SYSDATE
                WHERE REFILL_ID = :refill_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("status_message", statusMessage ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("refill_id", refillId));
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<List<RefillRequestItem>> GetPendingRefillsAsync()
        {
            var list = new List<RefillRequestItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT REFILL_ID, MR_NO, MEDICATION_ID, MEDICATION_NAME, PP_ID, PATIENT_VISIT_ID,
                       QUANTITY, NOTES, STATUS, STATUS_MESSAGE, CREATED_AT, UPDATED_AT
                FROM PATIENT_MED_REFILL_REQUEST
                WHERE STATUS IN ('PENDING', 'IN_REVIEW')
                ORDER BY CREATED_AT DESC", conn);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RefillRequestItem
                {
                    RefillId = Convert.ToInt32(reader["REFILL_ID"]),
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    MedicationId = reader["MEDICATION_ID"] != DBNull.Value ? Convert.ToInt32(reader["MEDICATION_ID"]) : null,
                    MedicationName = reader["MEDICATION_NAME"]?.ToString(),
                    PpId = reader["PP_ID"] != DBNull.Value ? Convert.ToInt32(reader["PP_ID"]) : null,
                    PatientVisitId = reader["PATIENT_VISIT_ID"] != DBNull.Value ? Convert.ToInt32(reader["PATIENT_VISIT_ID"]) : null,
                    Quantity = reader["QUANTITY"] != DBNull.Value ? Convert.ToInt32(reader["QUANTITY"]) : null,
                    Notes = reader["NOTES"]?.ToString(),
                    Status = reader["STATUS"]?.ToString() ?? "PENDING",
                    StatusMessage = reader["STATUS_MESSAGE"]?.ToString(),
                    CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                    UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
                });
            }
            return list;
        }

        private static SupportTicketDto MapTicket(OracleDataReader reader) => new()
        {
            TicketId = Convert.ToInt32(reader["TICKET_ID"]),
            MrNo = reader["MR_NO"]?.ToString(),
            ContactName = reader["CONTACT_NAME"]?.ToString() ?? string.Empty,
            ContactPhone = HasColumn(reader, "CONTACT_PHONE") ? reader["CONTACT_PHONE"]?.ToString() : null,
            ContactEmail = HasColumn(reader, "CONTACT_EMAIL") ? reader["CONTACT_EMAIL"]?.ToString() : null,
            Category = reader["CATEGORY"]?.ToString() ?? string.Empty,
            Subject = reader["SUBJECT"]?.ToString() ?? string.Empty,
            Description = reader["DESCRIPTION"]?.ToString() ?? string.Empty,
            Status = reader["STATUS"]?.ToString() ?? "OPEN",
            AdminNotes = reader["ADMIN_NOTES"]?.ToString(),
            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
            UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
        };

        private static bool HasColumn(OracleDataReader reader, string name)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public interface IAuditLogRepository
    {
        Task WriteAsync(AuditLogEntry entry);
        Task<List<AuditLogItem>> GetRecentAsync(int take, string? mrNo = null);
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IConfiguration _configuration;

        public AuditLogRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task WriteAsync(AuditLogEntry entry)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_AUDIT_LOG (ACTOR_ID, ACTOR_ROLE, ACTION, ENTITY_TYPE, ENTITY_ID, MR_NO, IP_ADDRESS, DETAILS)
                VALUES (:actor_id, :actor_role, :action, :entity_type, :entity_id, :mr_no, :ip_address, :details)", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("actor_id", entry.ActorId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("actor_role", entry.ActorRole ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("action", entry.Action));
            cmd.Parameters.Add(new OracleParameter("entity_type", entry.EntityType ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("entity_id", entry.EntityId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("mr_no", entry.MrNo ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("ip_address", entry.IpAddress ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("details", entry.Details ?? (object)DBNull.Value));
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<AuditLogItem>> GetRecentAsync(int take, string? mrNo = null)
        {
            var list = new List<AuditLogItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            var sql = @"
                SELECT * FROM (
                    SELECT AUDIT_ID, ACTOR_ID, ACTOR_ROLE, ACTION, ENTITY_TYPE, ENTITY_ID, MR_NO, IP_ADDRESS, DETAILS, CREATED_AT
                    FROM MOBILE_AUDIT_LOG";
            if (!string.IsNullOrWhiteSpace(mrNo)) sql += " WHERE MR_NO = :mr_no";
            sql += " ORDER BY CREATED_AT DESC) WHERE ROWNUM <= :take";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            if (!string.IsNullOrWhiteSpace(mrNo)) cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("take", take));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AuditLogItem
                {
                    AuditId = Convert.ToInt32(reader["AUDIT_ID"]),
                    ActorId = reader["ACTOR_ID"]?.ToString(),
                    ActorRole = reader["ACTOR_ROLE"]?.ToString(),
                    Action = reader["ACTION"]?.ToString() ?? string.Empty,
                    EntityType = reader["ENTITY_TYPE"]?.ToString(),
                    EntityId = reader["ENTITY_ID"]?.ToString(),
                    MrNo = reader["MR_NO"]?.ToString(),
                    IpAddress = reader["IP_ADDRESS"]?.ToString(),
                    Details = reader["DETAILS"]?.ToString(),
                    CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                });
            }
            return list;
        }
    }
}
