using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IAppPinRepository
    {
        Task<bool> HasActivePinAsync(string mrNo);
        Task UpsertPinAsync(string mrNo, string pinHash, string? deviceLabel);
        Task<bool> ClearPinAsync(string mrNo);
        Task<string?> GetPinHashAsync(string mrNo);
    }

    public class AppPinRepository : IAppPinRepository
    {
        private readonly IConfiguration _configuration;

        public AppPinRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> HasActivePinAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(1)
                FROM PATIENT_APP_PIN
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'
                  AND PIN_HASH IS NOT NULL", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }

        public async Task<string?> GetPinHashAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT PIN_HASH
                FROM PATIENT_APP_PIN
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            var hash = result?.ToString();
            return string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();
        }

        public async Task UpsertPinAsync(string mrNo, string pinHash, string? deviceLabel)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var merge = new OracleCommand(@"
                MERGE INTO PATIENT_APP_PIN t
                USING (SELECT :mr_no AS MR_NO FROM dual) s
                ON (t.MR_NO = s.MR_NO)
                WHEN MATCHED THEN
                    UPDATE SET
                        PIN_HASH = :pin_hash,
                        DEVICE_LABEL = :device_label,
                        UPDATED_AT = SYSDATE,
                        IS_ACTIVE = 'Y'
                WHEN NOT MATCHED THEN
                    INSERT (
                        PIN_ID,
                        MR_NO,
                        PIN_HASH,
                        DEVICE_LABEL,
                        CREATED_AT,
                        UPDATED_AT,
                        IS_ACTIVE
                    ) VALUES (
                        PATIENT_APP_PIN_SEQ.NEXTVAL,
                        :mr_no,
                        :pin_hash,
                        :device_label,
                        SYSDATE,
                        SYSDATE,
                        'Y'
                    )", conn);

            merge.BindByName = true;
            merge.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();
            merge.Parameters.Add("pin_hash", OracleDbType.Varchar2).Value = pinHash.Trim();
            merge.Parameters.Add("device_label", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(deviceLabel)
                    ? DBNull.Value
                    : deviceLabel.Trim();

            await merge.ExecuteNonQueryAsync();
        }

        public async Task<bool> ClearPinAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_APP_PIN
                   SET IS_ACTIVE = 'N',
                       UPDATED_AT = SYSDATE
                 WHERE MR_NO = :mr_no
                   AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }
}
