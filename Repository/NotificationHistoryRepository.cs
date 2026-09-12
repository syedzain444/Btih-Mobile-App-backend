using System.Text.Json;
using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class NotificationHistoryRepository : INotificationHistoryRepository
    {
        private readonly IConfiguration _configuration;

        public NotificationHistoryRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<int> InsertAsync(PatientNotificationRecord record)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO PATIENT_NOTIFICATION (
                    MR_NO,
                    NOTIFICATION_TYPE,
                    CATEGORY,
                    PRIORITY,
                    TITLE,
                    BODY,
                    PAYLOAD_JSON,
                    IS_READ,
                    CREATED_AT
                ) VALUES (
                    :mr_no,
                    :notification_type,
                    :category,
                    :priority,
                    :title,
                    :body,
                    :payload_json,
                    'N',
                    SYSDATE
                )
                RETURNING NOTIFICATION_ID INTO :notification_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", record.MrNo));
            cmd.Parameters.Add(new OracleParameter("notification_type", record.NotificationType));
            cmd.Parameters.Add(new OracleParameter("category", record.Category));
            cmd.Parameters.Add(new OracleParameter("priority", record.Priority));
            cmd.Parameters.Add(new OracleParameter("title", record.Title));
            cmd.Parameters.Add(new OracleParameter("body", record.Body));
            cmd.Parameters.Add(new OracleParameter(
                "payload_json",
                string.IsNullOrWhiteSpace(record.PayloadJson)
                    ? (object)DBNull.Value
                    : record.PayloadJson));

            var idOut = new OracleParameter("notification_id", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(idOut);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(idOut.Value.ToString());
        }

        public async Task<NotificationInboxResult> GetInboxAsync(
            string mrNo,
            int pageNumber,
            int pageSize,
            string? category)
        {
            var result = new NotificationInboxResult
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
            };

            var connStr = _configuration.GetConnectionString("HMISConnection");
            var normalizedCategory = NormalizeCategoryFilter(category);

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using (var countCmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_NOTIFICATION
                WHERE MR_NO = :mr_no
                  AND (:category IS NULL OR CATEGORY = :category)", conn))
            {
                countCmd.BindByName = true;
                countCmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
                countCmd.Parameters.Add(new OracleParameter(
                    "category",
                    normalizedCategory ?? (object)DBNull.Value));

                result.TotalRecords = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            result.TotalPages = result.TotalRecords == 0
                ? 0
                : (int)Math.Ceiling(result.TotalRecords / (double)pageSize);

            result.UnreadCount = await GetUnreadCountAsync(mrNo);

            var offset = (pageNumber - 1) * pageSize;

            await using var cmd = new OracleCommand(@"
                SELECT NOTIFICATION_ID, MR_NO, NOTIFICATION_TYPE, CATEGORY, PRIORITY,
                       TITLE, BODY, PAYLOAD_JSON, IS_READ, CREATED_AT, READ_AT
                FROM PATIENT_NOTIFICATION
                WHERE MR_NO = :mr_no
                  AND (:category IS NULL OR CATEGORY = :category)
                ORDER BY CREATED_AT DESC
                OFFSET :offset ROWS FETCH NEXT :page_size ROWS ONLY", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter(
                "category",
                normalizedCategory ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("offset", offset));
            cmd.Parameters.Add(new OracleParameter("page_size", pageSize));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Data.Add(MapRecord(reader));
            }

            return result;
        }

        public async Task<int> GetUnreadCountAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_NOTIFICATION
                WHERE MR_NO = :mr_no
                  AND IS_READ = 'N'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_NOTIFICATION
                SET IS_READ = 'Y',
                    READ_AT = SYSDATE
                WHERE NOTIFICATION_ID = :notification_id
                  AND MR_NO = :mr_no
                  AND IS_READ = 'N'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("notification_id", notificationId));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<int> MarkAllAsReadAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_NOTIFICATION
                SET IS_READ = 'Y',
                    READ_AT = SYSDATE
                WHERE MR_NO = :mr_no
                  AND IS_READ = 'N'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        private static PatientNotificationRecord MapRecord(OracleDataReader reader)
        {
            return new PatientNotificationRecord
            {
                NotificationId = Convert.ToInt32(reader["NOTIFICATION_ID"]),
                MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                NotificationType = reader["NOTIFICATION_TYPE"]?.ToString() ?? string.Empty,
                Category = reader["CATEGORY"]?.ToString() ?? "general",
                Priority = reader["PRIORITY"]?.ToString() ?? "normal",
                Title = reader["TITLE"]?.ToString() ?? string.Empty,
                Body = reader["BODY"]?.ToString() ?? string.Empty,
                PayloadJson = reader["PAYLOAD_JSON"] == DBNull.Value
                    ? null
                    : reader["PAYLOAD_JSON"]?.ToString(),
                IsRead = (reader["IS_READ"]?.ToString() ?? "N") == "Y",
                CreatedAt = reader["CREATED_AT"] is DateTime created
                    ? created
                    : DateTime.Now,
                ReadAt = reader["READ_AT"] == DBNull.Value
                    ? null
                    : reader["READ_AT"] as DateTime?,
            };
        }

        private static string? NormalizeCategoryFilter(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return null;
            }

            var normalized = category.Trim().ToLowerInvariant();
            return normalized switch
            {
                "all" => null,
                "appointments" => "appointments",
                "lab" => "lab",
                "records" => "records",
                "billing" => "billing",
                "general" => "general",
                _ => null,
            };
        }

        public static string SerializePayload(Dictionary<string, string>? payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(payload);
        }
    }
}
