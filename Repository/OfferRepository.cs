using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IOfferRepository
    {
        Task<List<MobileOfferRecord>> GetAllAsync();
        Task<List<MobileOfferRecord>> GetActiveAsync();
        Task<MobileOfferRecord?> GetByIdAsync(int offerId);
        Task<MobileOfferRecord> InsertAsync(MobileOfferRecord record);
        Task<bool> UpdateAsync(MobileOfferRecord record);
        Task<bool> DeleteAsync(int offerId);
    }

    public class OfferRepository : IOfferRepository
    {
        private readonly IConfiguration _configuration;

        public OfferRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<MobileOfferRecord>> GetAllAsync()
        {
            var result = new List<MobileOfferRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT OFFER_ID, TITLE, SUBTITLE, DESCRIPTION, CATEGORY, IMAGE_URL,
                       ORIGINAL_PRICE, OFFER_PRICE, CURRENCY, HIGHLIGHTS, CTA_LABEL, CTA_PHONE,
                       SORT_ORDER, IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                FROM MOBILE_OFFER
                ORDER BY SORT_ORDER ASC, OFFER_ID DESC", conn);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(Map(reader));
            }

            return result;
        }

        public async Task<List<MobileOfferRecord>> GetActiveAsync()
        {
            var result = new List<MobileOfferRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT OFFER_ID, TITLE, SUBTITLE, DESCRIPTION, CATEGORY, IMAGE_URL,
                       ORIGINAL_PRICE, OFFER_PRICE, CURRENCY, HIGHLIGHTS, CTA_LABEL, CTA_PHONE,
                       SORT_ORDER, IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                FROM MOBILE_OFFER
                WHERE IS_ACTIVE = 'Y'
                  AND (START_AT IS NULL OR START_AT <= SYSDATE)
                  AND (END_AT IS NULL OR END_AT >= SYSDATE)
                ORDER BY SORT_ORDER ASC, OFFER_ID DESC", conn);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(Map(reader));
            }

            return result;
        }

        public async Task<MobileOfferRecord?> GetByIdAsync(int offerId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT OFFER_ID, TITLE, SUBTITLE, DESCRIPTION, CATEGORY, IMAGE_URL,
                       ORIGINAL_PRICE, OFFER_PRICE, CURRENCY, HIGHLIGHTS, CTA_LABEL, CTA_PHONE,
                       SORT_ORDER, IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                FROM MOBILE_OFFER
                WHERE OFFER_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = offerId;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return Map(reader);
        }

        public async Task<MobileOfferRecord> InsertAsync(MobileOfferRecord record)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            int id;
            await using (var seqCmd = new OracleCommand(
                "SELECT MOBILE_OFFER_SEQ.NEXTVAL FROM DUAL", conn))
            {
                id = Convert.ToInt32((await seqCmd.ExecuteScalarAsync())!.ToString());
            }

            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_OFFER (
                    OFFER_ID, TITLE, SUBTITLE, DESCRIPTION, CATEGORY, IMAGE_URL,
                    ORIGINAL_PRICE, OFFER_PRICE, CURRENCY, HIGHLIGHTS, CTA_LABEL, CTA_PHONE,
                    SORT_ORDER, IS_ACTIVE, START_AT, END_AT, CREATED_AT, UPDATED_AT
                ) VALUES (
                    :id, :title, :subtitle, :description, :category, :image_url,
                    :original_price, :offer_price, :currency, :highlights, :cta_label, :cta_phone,
                    :sort_order, :is_active, :start_at, :end_at, SYSDATE, SYSDATE
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;
            BindCommon(cmd, record);
            await cmd.ExecuteNonQueryAsync();

            return (await GetByIdAsync(id))!;
        }

        public async Task<bool> UpdateAsync(MobileOfferRecord record)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_OFFER
                SET TITLE = :title,
                    SUBTITLE = :subtitle,
                    DESCRIPTION = :description,
                    CATEGORY = :category,
                    IMAGE_URL = :image_url,
                    ORIGINAL_PRICE = :original_price,
                    OFFER_PRICE = :offer_price,
                    CURRENCY = :currency,
                    HIGHLIGHTS = :highlights,
                    CTA_LABEL = :cta_label,
                    CTA_PHONE = :cta_phone,
                    SORT_ORDER = :sort_order,
                    IS_ACTIVE = :is_active,
                    START_AT = :start_at,
                    END_AT = :end_at,
                    UPDATED_AT = SYSDATE
                WHERE OFFER_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = record.OfferId;
            BindCommon(cmd, record);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int offerId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(
                "DELETE FROM MOBILE_OFFER WHERE OFFER_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = offerId;

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static void BindCommon(OracleCommand cmd, MobileOfferRecord record)
        {
            cmd.Parameters.Add("title", OracleDbType.Varchar2).Value = Truncate(record.Title.Trim(), 160);
            cmd.Parameters.Add("subtitle", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(record.Subtitle) ? DBNull.Value : Truncate(record.Subtitle.Trim(), 200);
            cmd.Parameters.Add("description", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(record.Description) ? DBNull.Value : Truncate(record.Description.Trim(), 2000);
            cmd.Parameters.Add("category", OracleDbType.Varchar2).Value =
                Truncate(string.IsNullOrWhiteSpace(record.Category) ? "Package" : record.Category.Trim(), 60);
            cmd.Parameters.Add("image_url", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(record.ImageUrl) ? DBNull.Value : Truncate(record.ImageUrl.Trim(), 500);
            cmd.Parameters.Add("original_price", OracleDbType.Decimal).Value =
                record.OriginalPrice.HasValue ? record.OriginalPrice.Value : DBNull.Value;
            cmd.Parameters.Add("offer_price", OracleDbType.Decimal).Value =
                record.OfferPrice.HasValue ? record.OfferPrice.Value : DBNull.Value;
            cmd.Parameters.Add("currency", OracleDbType.Varchar2).Value =
                Truncate(string.IsNullOrWhiteSpace(record.Currency) ? "PKR" : record.Currency.Trim(), 10);
            cmd.Parameters.Add("highlights", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(record.Highlights) ? DBNull.Value : Truncate(record.Highlights.Trim(), 2000);
            cmd.Parameters.Add("cta_label", OracleDbType.Varchar2).Value =
                Truncate(string.IsNullOrWhiteSpace(record.CtaLabel) ? "Enquire" : record.CtaLabel.Trim(), 60);
            cmd.Parameters.Add("cta_phone", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(record.CtaPhone) ? DBNull.Value : Truncate(record.CtaPhone.Trim(), 30);
            cmd.Parameters.Add("sort_order", OracleDbType.Int32).Value = record.SortOrder;
            cmd.Parameters.Add("is_active", OracleDbType.Char).Value = record.IsActive ? "Y" : "N";
            cmd.Parameters.Add("start_at", OracleDbType.Date).Value =
                record.StartAt.HasValue ? record.StartAt.Value : DBNull.Value;
            cmd.Parameters.Add("end_at", OracleDbType.Date).Value =
                record.EndAt.HasValue ? record.EndAt.Value : DBNull.Value;
        }

        private static MobileOfferRecord Map(OracleDataReader reader)
        {
            return new MobileOfferRecord
            {
                OfferId = Convert.ToInt32(reader["OFFER_ID"]),
                Title = reader["TITLE"]?.ToString() ?? string.Empty,
                Subtitle = reader["SUBTITLE"] == DBNull.Value ? null : reader["SUBTITLE"]?.ToString(),
                Description = reader["DESCRIPTION"] == DBNull.Value ? null : reader["DESCRIPTION"]?.ToString(),
                Category = reader["CATEGORY"]?.ToString() ?? "Package",
                ImageUrl = reader["IMAGE_URL"] == DBNull.Value ? null : reader["IMAGE_URL"]?.ToString(),
                OriginalPrice = reader["ORIGINAL_PRICE"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["ORIGINAL_PRICE"]),
                OfferPrice = reader["OFFER_PRICE"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["OFFER_PRICE"]),
                Currency = reader["CURRENCY"]?.ToString() ?? "PKR",
                Highlights = reader["HIGHLIGHTS"] == DBNull.Value ? null : reader["HIGHLIGHTS"]?.ToString(),
                CtaLabel = reader["CTA_LABEL"]?.ToString() ?? "Enquire",
                CtaPhone = reader["CTA_PHONE"] == DBNull.Value ? null : reader["CTA_PHONE"]?.ToString(),
                SortOrder = Convert.ToInt32(reader["SORT_ORDER"]),
                IsActive = (reader["IS_ACTIVE"]?.ToString() ?? "N") == "Y",
                StartAt = reader["START_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["START_AT"]),
                EndAt = reader["END_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["END_AT"]),
                CreatedAt = reader["CREATED_AT"] == DBNull.Value
                    ? DateTime.Now
                    : Convert.ToDateTime(reader["CREATED_AT"]),
                UpdatedAt = reader["UPDATED_AT"] == DBNull.Value
                    ? DateTime.Now
                    : Convert.ToDateTime(reader["UPDATED_AT"]),
            };
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
