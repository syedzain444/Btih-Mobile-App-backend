using System.Text.Json;
using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class RecentActivityRepository : IRecentActivityRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly IConfiguration _configuration;

        public RecentActivityRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static string? SerializePayload(object? payload)
        {
            if (payload == null)
            {
                return null;
            }

            if (payload is string s)
            {
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }

            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        public async Task<PatientRecentActivityRecord> UpsertAsync(PatientRecentActivityRecord record)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using (var updateCmd = new OracleCommand(@"
                UPDATE PATIENT_RECENT_ACTIVITY
                   SET KIND = :kind,
                       TITLE = :title,
                       SUBTITLE = :subtitle,
                       PAYLOAD_JSON = :payload_json,
                       VIEWED_AT = :viewed_at
                 WHERE MR_NO = :mr_no
                   AND ACTIVITY_KEY = :activity_key", conn))
            {
                updateCmd.BindByName = true;
                BindRecord(updateCmd, record);
                var updated = await updateCmd.ExecuteNonQueryAsync();
                if (updated > 0)
                {
                    return await GetByKeyAsync(conn, record.MrNo, record.ActivityKey)
                           ?? record;
                }
            }

            await using (var insertCmd = new OracleCommand(@"
                INSERT INTO PATIENT_RECENT_ACTIVITY (
                    MR_NO,
                    ACTIVITY_KEY,
                    KIND,
                    TITLE,
                    SUBTITLE,
                    PAYLOAD_JSON,
                    VIEWED_AT
                ) VALUES (
                    :mr_no,
                    :activity_key,
                    :kind,
                    :title,
                    :subtitle,
                    :payload_json,
                    :viewed_at
                )
                RETURNING ACTIVITY_ID INTO :activity_id", conn))
            {
                insertCmd.BindByName = true;
                BindRecord(insertCmd, record);
                var idOut = new OracleParameter("activity_id", OracleDbType.Int32)
                {
                    Direction = System.Data.ParameterDirection.Output,
                };
                insertCmd.Parameters.Add(idOut);
                await insertCmd.ExecuteNonQueryAsync();
                record.ActivityId = Convert.ToInt32(idOut.Value.ToString());
            }

            return record;
        }

        public async Task<List<PatientRecentActivityRecord>> GetLatestAsync(string mrNo, int limit)
        {
            var list = new List<PatientRecentActivityRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(@"
                SELECT * FROM (
                    SELECT ACTIVITY_ID,
                           MR_NO,
                           ACTIVITY_KEY,
                           KIND,
                           TITLE,
                           SUBTITLE,
                           PAYLOAD_JSON,
                           VIEWED_AT
                      FROM PATIENT_RECENT_ACTIVITY
                     WHERE MR_NO = :mr_no
                     ORDER BY VIEWED_AT DESC, ACTIVITY_ID DESC
                )
                WHERE ROWNUM <= :row_limit", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("row_limit", limit));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(Map(reader));
            }

            return list;
        }

        public async Task TrimToLimitAsync(string mrNo, int limit)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(@"
                DELETE FROM PATIENT_RECENT_ACTIVITY
                 WHERE MR_NO = :mr_no
                   AND ACTIVITY_ID NOT IN (
                        SELECT ACTIVITY_ID FROM (
                            SELECT ACTIVITY_ID
                              FROM PATIENT_RECENT_ACTIVITY
                             WHERE MR_NO = :mr_no
                             ORDER BY VIEWED_AT DESC, ACTIVITY_ID DESC
                        )
                        WHERE ROWNUM <= :row_limit
                   )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("row_limit", limit));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> ClearAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(@"
                DELETE FROM PATIENT_RECENT_ACTIVITY
                 WHERE MR_NO = :mr_no", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            return await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<PatientRecentActivityRecord?> GetByKeyAsync(
            OracleConnection conn,
            string mrNo,
            string activityKey)
        {
            await using var cmd = new OracleCommand(@"
                SELECT ACTIVITY_ID,
                       MR_NO,
                       ACTIVITY_KEY,
                       KIND,
                       TITLE,
                       SUBTITLE,
                       PAYLOAD_JSON,
                       VIEWED_AT
                  FROM PATIENT_RECENT_ACTIVITY
                 WHERE MR_NO = :mr_no
                   AND ACTIVITY_KEY = :activity_key", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("activity_key", activityKey));

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return Map(reader);
        }

        private static void BindRecord(OracleCommand cmd, PatientRecentActivityRecord record)
        {
            cmd.Parameters.Add(new OracleParameter("mr_no", record.MrNo));
            cmd.Parameters.Add(new OracleParameter("activity_key", record.ActivityKey));
            cmd.Parameters.Add(new OracleParameter("kind", record.Kind));
            cmd.Parameters.Add(new OracleParameter("title", record.Title));
            cmd.Parameters.Add(new OracleParameter(
                "subtitle",
                string.IsNullOrWhiteSpace(record.Subtitle)
                    ? (object)DBNull.Value
                    : record.Subtitle));
            cmd.Parameters.Add(new OracleParameter(
                "payload_json",
                string.IsNullOrWhiteSpace(record.PayloadJson)
                    ? (object)DBNull.Value
                    : record.PayloadJson));
            cmd.Parameters.Add(new OracleParameter("viewed_at", record.ViewedAt));
        }

        private static PatientRecentActivityRecord Map(Oracle.ManagedDataAccess.Client.OracleDataReader reader)
        {
            return new PatientRecentActivityRecord
            {
                ActivityId = Convert.ToInt32(reader["ACTIVITY_ID"]),
                MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                ActivityKey = reader["ACTIVITY_KEY"]?.ToString() ?? string.Empty,
                Kind = reader["KIND"]?.ToString() ?? string.Empty,
                Title = reader["TITLE"]?.ToString() ?? string.Empty,
                Subtitle = reader["SUBTITLE"] == DBNull.Value
                    ? string.Empty
                    : reader["SUBTITLE"]?.ToString() ?? string.Empty,
                PayloadJson = reader["PAYLOAD_JSON"] == DBNull.Value
                    ? null
                    : reader["PAYLOAD_JSON"]?.ToString(),
                ViewedAt = Convert.ToDateTime(reader["VIEWED_AT"]),
            };
        }
    }
}
