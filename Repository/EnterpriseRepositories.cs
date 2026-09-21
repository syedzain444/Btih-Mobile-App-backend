using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IPaymentRepository
    {
        Task<int> CreateIntentAsync(PaymentIntentDto intent);
        Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null);
        Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo);
        Task<bool> UpdateStatusAsync(int paymentId, string status, string? gatewayRef, string? failureReason);
        Task AddTransactionAsync(int paymentId, string eventType, string? gatewayRef, string? rawPayload);
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
                SELECT TICKET_ID, MR_NO, CONTACT_NAME, CATEGORY, SUBJECT, DESCRIPTION, STATUS, ADMIN_NOTES, CREATED_AT, UPDATED_AT
                FROM MOBILE_SUPPORT_TICKET
                WHERE STATUS IN ('OPEN', 'IN_PROGRESS')
                ORDER BY CREATED_AT DESC", conn);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapTicket(reader));
            return list;
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
            Category = reader["CATEGORY"]?.ToString() ?? string.Empty,
            Subject = reader["SUBJECT"]?.ToString() ?? string.Empty,
            Description = reader["DESCRIPTION"]?.ToString() ?? string.Empty,
            Status = reader["STATUS"]?.ToString() ?? "OPEN",
            AdminNotes = reader["ADMIN_NOTES"]?.ToString(),
            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
            UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
        };
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
