using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IPromotionRepository
    {
        Task<List<MobilePromotionRecord>> GetAllAsync();
        Task<List<MobilePromotionRecord>> GetActiveAsync();
        Task<MobilePromotionRecord?> GetByIdAsync(int promotionId);
        Task<MobilePromotionRecord> InsertAsync(MobilePromotionRecord record);
        Task<bool> UpdateAsync(MobilePromotionRecord record);
        Task<bool> DeleteAsync(int promotionId);
    }

    public class PromotionRepository : IPromotionRepository
    {
        private readonly IConfiguration _configuration;

        public PromotionRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<MobilePromotionRecord>> GetAllAsync()
        {
            var result = new List<MobilePromotionRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT PROMOTION_ID, TITLE, IMAGE_URL, SORT_ORDER, DURATION_SECONDS,
                       IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                FROM MOBILE_PROMOTION
                ORDER BY SORT_ORDER ASC, PROMOTION_ID DESC", conn);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(Map(reader));
            }

            return result;
        }

        public async Task<List<MobilePromotionRecord>> GetActiveAsync()
        {
            var result = new List<MobilePromotionRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT PROMOTION_ID, TITLE, IMAGE_URL, SORT_ORDER, DURATION_SECONDS,
                       IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                FROM MOBILE_PROMOTION
                WHERE IS_ACTIVE = 'Y'
                  AND (START_AT IS NULL OR START_AT <= SYSDATE)
                  AND (END_AT IS NULL OR END_AT >= SYSDATE)
                ORDER BY SORT_ORDER ASC, PROMOTION_ID DESC", conn);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(Map(reader));
            }

            return result;
        }

        public async Task<MobilePromotionRecord?> GetByIdAsync(int promotionId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT PROMOTION_ID, TITLE, IMAGE_URL, SORT_ORDER, DURATION_SECONDS,
                       IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                FROM MOBILE_PROMOTION
                WHERE PROMOTION_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = promotionId;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return Map(reader);
        }

        public async Task<MobilePromotionRecord> InsertAsync(MobilePromotionRecord record)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            int id;
            await using (var seqCmd = new OracleCommand(
                "SELECT MOBILE_PROMOTION_SEQ.NEXTVAL FROM DUAL", conn))
            {
                id = Convert.ToInt32((await seqCmd.ExecuteScalarAsync())!.ToString());
            }

            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_PROMOTION (
                    PROMOTION_ID, TITLE, IMAGE_URL, SORT_ORDER, DURATION_SECONDS,
                    IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                ) VALUES (
                    :id, :title, :image_url, :sort_order, :duration_seconds,
                    :is_active, :start_at, :end_at, SYSDATE, SYSDATE
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;
            BindCommon(cmd, record);
            await cmd.ExecuteNonQueryAsync();

            return (await GetByIdAsync(id))!;
        }

        public async Task<bool> UpdateAsync(MobilePromotionRecord record)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_PROMOTION
                SET TITLE = :title,
                    IMAGE_URL = :image_url,
                    SORT_ORDER = :sort_order,
                    DURATION_SECONDS = :duration_seconds,
                    IS_ACTIVE = :is_active,
                    START_AT = :start_at,
                    END_AT = :end_at,
                    UPDATED_AT = SYSDATE
                WHERE PROMOTION_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = record.PromotionId;
            BindCommon(cmd, record);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int promotionId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                DELETE FROM MOBILE_PROMOTION WHERE PROMOTION_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = promotionId;

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static void BindCommon(OracleCommand cmd, MobilePromotionRecord record)
        {
            cmd.Parameters.Add("title", OracleDbType.Varchar2).Value = Truncate(record.Title.Trim(), 120);
            cmd.Parameters.Add("image_url", OracleDbType.Varchar2).Value = Truncate(record.ImageUrl.Trim(), 500);
            cmd.Parameters.Add("sort_order", OracleDbType.Int32).Value = record.SortOrder;
            cmd.Parameters.Add("duration_seconds", OracleDbType.Int32).Value =
                record.DurationSeconds <= 0 ? 5 : Math.Min(record.DurationSeconds, 60);
            cmd.Parameters.Add("is_active", OracleDbType.Char).Value = record.IsActive ? "Y" : "N";
            cmd.Parameters.Add("start_at", OracleDbType.Date).Value =
                record.StartAt.HasValue ? record.StartAt.Value : DBNull.Value;
            cmd.Parameters.Add("end_at", OracleDbType.Date).Value =
                record.EndAt.HasValue ? record.EndAt.Value : DBNull.Value;
        }

        private static MobilePromotionRecord Map(OracleDataReader reader)
        {
            return new MobilePromotionRecord
            {
                PromotionId = Convert.ToInt32(reader["PROMOTION_ID"]),
                Title = reader["TITLE"]?.ToString() ?? string.Empty,
                ImageUrl = reader["IMAGE_URL"]?.ToString() ?? string.Empty,
                SortOrder = Convert.ToInt32(reader["SORT_ORDER"]),
                DurationSeconds = Convert.ToInt32(reader["DURATION_SECONDS"]),
                IsActive = (reader["IS_ACTIVE"]?.ToString() ?? "N") == "Y",
                StartAt = reader["START_AT"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["START_AT"]),
                EndAt = reader["END_AT"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["END_AT"]),
                CreatedAt = reader["CREATED_AT"] == DBNull.Value
                    ? DateTime.UtcNow
                    : Convert.ToDateTime(reader["CREATED_AT"]),
                UpdatedAt = reader["UPDATED_AT"] == DBNull.Value
                    ? DateTime.UtcNow
                    : Convert.ToDateTime(reader["UPDATED_AT"]),
            };
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
