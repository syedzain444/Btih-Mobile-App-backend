using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface ITelemedicineRepository
    {
        Task<int> CreateSessionAsync(TelemedSessionDto session);
        Task<TelemedSessionDto?> GetSessionAsync(int sessionId, string? mrNo = null);
        Task<List<TelemedSessionDto>> GetSessionsByMrNoAsync(string mrNo);
        Task<bool> UpdateStatusAsync(int sessionId, string status);
    }

    public class TelemedicineRepository : ITelemedicineRepository
    {
        private readonly IConfiguration _configuration;

        public TelemedicineRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task<int> CreateSessionAsync(TelemedSessionDto session)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO TELEMED_SESSION (
                    MR_NO, APPOINTMENT_ID, DOCTOR_ID, DOCTOR_NAME, ROOM_ID,
                    PATIENT_TOKEN, DOCTOR_TOKEN, STATUS, SCHEDULED_AT
                ) VALUES (
                    :mr_no, :appointment_id, :doctor_id, :doctor_name, :room_id,
                    :patient_token, :doctor_token, :status, :scheduled_at
                ) RETURNING SESSION_ID INTO :session_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", session.MrNo));
            cmd.Parameters.Add(new OracleParameter("appointment_id", session.AppointmentId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("doctor_id", session.DoctorId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("doctor_name", session.DoctorName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("room_id", session.RoomId));
            cmd.Parameters.Add(new OracleParameter("patient_token", session.PatientToken ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("doctor_token", (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("status", session.Status));
            cmd.Parameters.Add(new OracleParameter("scheduled_at", session.ScheduledAt ?? (object)DBNull.Value));

            var outParam = new OracleParameter("session_id", OracleDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(outParam.Value.ToString());
        }

        public async Task<TelemedSessionDto?> GetSessionAsync(int sessionId, string? mrNo = null)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            var sql = @"
                SELECT SESSION_ID, MR_NO, APPOINTMENT_ID, DOCTOR_ID, DOCTOR_NAME, ROOM_ID,
                       PATIENT_TOKEN, STATUS, SCHEDULED_AT, STARTED_AT, ENDED_AT
                FROM TELEMED_SESSION WHERE SESSION_ID = :session_id";
            if (!string.IsNullOrWhiteSpace(mrNo)) sql += " AND MR_NO = :mr_no";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("session_id", sessionId));
            if (!string.IsNullOrWhiteSpace(mrNo)) cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapSession(reader) : null;
        }

        public async Task<List<TelemedSessionDto>> GetSessionsByMrNoAsync(string mrNo)
        {
            var list = new List<TelemedSessionDto>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT SESSION_ID, MR_NO, APPOINTMENT_ID, DOCTOR_ID, DOCTOR_NAME, ROOM_ID,
                       PATIENT_TOKEN, STATUS, SCHEDULED_AT, STARTED_AT, ENDED_AT
                FROM TELEMED_SESSION WHERE MR_NO = :mr_no ORDER BY CREATED_AT DESC", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapSession(reader));
            return list;
        }

        public async Task<bool> UpdateStatusAsync(int sessionId, string status)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE TELEMED_SESSION
                SET STATUS = :status,
                    STARTED_AT = CASE WHEN :status = 'ACTIVE' AND STARTED_AT IS NULL THEN SYSDATE ELSE STARTED_AT END,
                    ENDED_AT = CASE WHEN :status IN ('COMPLETED','CANCELLED') THEN SYSDATE ELSE ENDED_AT END,
                    UPDATED_AT = SYSDATE
                WHERE SESSION_ID = :session_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("session_id", sessionId));
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static TelemedSessionDto MapSession(OracleDataReader reader) => new()
        {
            SessionId = Convert.ToInt32(reader["SESSION_ID"]),
            MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
            AppointmentId = reader["APPOINTMENT_ID"]?.ToString(),
            DoctorId = reader["DOCTOR_ID"] != DBNull.Value ? Convert.ToInt32(reader["DOCTOR_ID"]) : null,
            DoctorName = reader["DOCTOR_NAME"]?.ToString(),
            RoomId = reader["ROOM_ID"]?.ToString() ?? string.Empty,
            PatientToken = reader["PATIENT_TOKEN"]?.ToString(),
            Status = reader["STATUS"]?.ToString() ?? string.Empty,
            ScheduledAt = reader["SCHEDULED_AT"] != DBNull.Value ? Convert.ToDateTime(reader["SCHEDULED_AT"]) : null,
            StartedAt = reader["STARTED_AT"] != DBNull.Value ? Convert.ToDateTime(reader["STARTED_AT"]) : null,
            EndedAt = reader["ENDED_AT"] != DBNull.Value ? Convert.ToDateTime(reader["ENDED_AT"]) : null,
        };
    }

    public interface IContentRepository
    {
        Task<List<LocalizedContentItem>> GetByLanguageAsync(string langCode);
        Task<LocalizedContentItem?> GetItemAsync(string contentKey, string langCode);
    }

    public class ContentRepository : IContentRepository
    {
        private readonly IConfiguration _configuration;

        public ContentRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task<List<LocalizedContentItem>> GetByLanguageAsync(string langCode)
        {
            var list = new List<LocalizedContentItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT CONTENT_KEY, LANG_CODE, TITLE, BODY
                FROM MOBILE_CONTENT_LOCALIZED
                WHERE LANG_CODE = :lang_code
                ORDER BY CONTENT_KEY", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("lang_code", langCode.ToLowerInvariant()));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LocalizedContentItem
                {
                    ContentKey = reader["CONTENT_KEY"]?.ToString() ?? string.Empty,
                    LangCode = reader["LANG_CODE"]?.ToString() ?? string.Empty,
                    Title = reader["TITLE"]?.ToString(),
                    Body = reader["BODY"]?.ToString(),
                });
            }
            return list;
        }

        public async Task<LocalizedContentItem?> GetItemAsync(string contentKey, string langCode)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT CONTENT_KEY, LANG_CODE, TITLE, BODY
                FROM MOBILE_CONTENT_LOCALIZED
                WHERE CONTENT_KEY = :content_key AND LANG_CODE = :lang_code", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("content_key", contentKey));
            cmd.Parameters.Add(new OracleParameter("lang_code", langCode.ToLowerInvariant()));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new LocalizedContentItem
            {
                ContentKey = reader["CONTENT_KEY"]?.ToString() ?? string.Empty,
                LangCode = reader["LANG_CODE"]?.ToString() ?? string.Empty,
                Title = reader["TITLE"]?.ToString(),
                Body = reader["BODY"]?.ToString(),
            };
        }
    }

    public interface ISupportRepository
    {
        Task<List<FaqItem>> GetFaqAsync(string langCode, string? category);
        Task<int> CreateTicketAsync(CreateSupportTicketRequest request);
        Task<SupportTicketDto?> GetTicketAsync(int ticketId, string? mrNo);
        Task<List<SupportTicketDto>> GetTicketsByMrNoAsync(string mrNo);
    }

    public class SupportRepository : ISupportRepository
    {
        private readonly IConfiguration _configuration;

        public SupportRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task<List<FaqItem>> GetFaqAsync(string langCode, string? category)
        {
            var list = new List<FaqItem>();
            var isUrdu = langCode.Equals("ur", StringComparison.OrdinalIgnoreCase);
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            var sql = @"
                SELECT FAQ_ID, CATEGORY,
                       CASE WHEN :use_urdu = 1 AND QUESTION_UR IS NOT NULL THEN QUESTION_UR ELSE QUESTION_EN END AS QUESTION,
                       CASE WHEN :use_urdu = 1 AND ANSWER_UR IS NOT NULL THEN ANSWER_UR ELSE ANSWER_EN END AS ANSWER
                FROM MOBILE_FAQ
                WHERE IS_ACTIVE = 'Y'";
            if (!string.IsNullOrWhiteSpace(category)) sql += " AND CATEGORY = :category";
            sql += " ORDER BY SORT_ORDER, FAQ_ID";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("use_urdu", isUrdu ? 1 : 0));
            if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.Add(new OracleParameter("category", category));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FaqItem
                {
                    FaqId = Convert.ToInt32(reader["FAQ_ID"]),
                    Category = reader["CATEGORY"]?.ToString() ?? string.Empty,
                    Question = reader["QUESTION"]?.ToString() ?? string.Empty,
                    Answer = reader["ANSWER"]?.ToString() ?? string.Empty,
                });
            }
            return list;
        }

        public async Task<int> CreateTicketAsync(CreateSupportTicketRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_SUPPORT_TICKET (
                    MR_NO, CONTACT_NAME, CONTACT_PHONE, CONTACT_EMAIL, CATEGORY, SUBJECT, DESCRIPTION
                ) VALUES (
                    :mr_no, :contact_name, :contact_phone, :contact_email, :category, :subject, :description
                ) RETURNING TICKET_ID INTO :ticket_id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("contact_name", request.ContactName));
            cmd.Parameters.Add(new OracleParameter("contact_phone", request.ContactPhone ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("contact_email", request.ContactEmail ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("category", request.Category));
            cmd.Parameters.Add(new OracleParameter("subject", request.Subject));
            cmd.Parameters.Add(new OracleParameter("description", request.Description));
            var outParam = new OracleParameter("ticket_id", OracleDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(outParam.Value.ToString());
        }

        public async Task<SupportTicketDto?> GetTicketAsync(int ticketId, string? mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            var sql = @"
                SELECT TICKET_ID, MR_NO, CONTACT_NAME, CATEGORY, SUBJECT, DESCRIPTION, STATUS, ADMIN_NOTES, CREATED_AT, UPDATED_AT
                FROM MOBILE_SUPPORT_TICKET WHERE TICKET_ID = :ticket_id";
            if (!string.IsNullOrWhiteSpace(mrNo)) sql += " AND MR_NO = :mr_no";
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("ticket_id", ticketId));
            if (!string.IsNullOrWhiteSpace(mrNo)) cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new SupportTicketDto
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

        public async Task<List<SupportTicketDto>> GetTicketsByMrNoAsync(string mrNo)
        {
            var list = new List<SupportTicketDto>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT TICKET_ID, MR_NO, CONTACT_NAME, CATEGORY, SUBJECT, DESCRIPTION, STATUS, ADMIN_NOTES, CREATED_AT, UPDATED_AT
                FROM MOBILE_SUPPORT_TICKET WHERE MR_NO = :mr_no ORDER BY CREATED_AT DESC", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SupportTicketDto
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
                });
            }
            return list;
        }
    }
}
