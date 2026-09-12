using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class TrustedDeviceRepository : ITrustedDeviceRepository
    {
        private readonly IConfiguration _configuration;

        public TrustedDeviceRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> IsTrustedAsync(
            string mrNo,
            string deviceInstallId,
            string trustTokenHash)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_TRUSTED_DEVICE
                WHERE MR_NO = :mr_no
                  AND DEVICE_INSTALL_ID = :device_install_id
                  AND TRUST_TOKEN_HASH = :trust_token_hash
                  AND IS_ACTIVE = 'Y'
                  AND EXPIRES_AT >= SYSDATE", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("device_install_id", deviceInstallId));
            cmd.Parameters.Add(new OracleParameter("trust_token_hash", trustTokenHash));

            await conn.OpenAsync();
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<string> UpsertTrustedDeviceAsync(
            string mrNo,
            string deviceInstallId,
            string trustTokenHash,
            string? deviceLabel,
            string? platform,
            DateTime expiresAt)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                MERGE INTO PATIENT_TRUSTED_DEVICE t
                USING (
                    SELECT :mr_no AS MR_NO, :device_install_id AS DEVICE_INSTALL_ID FROM dual
                ) s
                ON (t.MR_NO = s.MR_NO AND t.DEVICE_INSTALL_ID = s.DEVICE_INSTALL_ID)
                WHEN MATCHED THEN
                    UPDATE SET
                        TRUST_TOKEN_HASH = :trust_token_hash,
                        DEVICE_LABEL = :device_label,
                        PLATFORM = :platform,
                        TRUSTED_AT = SYSDATE,
                        LAST_LOGIN_AT = SYSDATE,
                        EXPIRES_AT = :expires_at,
                        IS_ACTIVE = 'Y'
                WHEN NOT MATCHED THEN
                    INSERT (
                        MR_NO,
                        DEVICE_INSTALL_ID,
                        TRUST_TOKEN_HASH,
                        DEVICE_LABEL,
                        PLATFORM,
                        TRUSTED_AT,
                        LAST_LOGIN_AT,
                        EXPIRES_AT,
                        IS_ACTIVE
                    ) VALUES (
                        :mr_no,
                        :device_install_id,
                        :trust_token_hash,
                        :device_label,
                        :platform,
                        SYSDATE,
                        SYSDATE,
                        :expires_at,
                        'Y'
                    )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("device_install_id", deviceInstallId));
            cmd.Parameters.Add(new OracleParameter("trust_token_hash", trustTokenHash));
            cmd.Parameters.Add(new OracleParameter(
                "device_label",
                string.IsNullOrWhiteSpace(deviceLabel) ? (object)DBNull.Value : deviceLabel.Trim()));
            cmd.Parameters.Add(new OracleParameter(
                "platform",
                string.IsNullOrWhiteSpace(platform) ? (object)DBNull.Value : platform.Trim()));
            cmd.Parameters.Add(new OracleParameter("expires_at", expiresAt));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return deviceInstallId;
        }

        public async Task TouchTrustedLoginAsync(
            string mrNo,
            string deviceInstallId,
            DateTime expiresAt)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_TRUSTED_DEVICE
                SET LAST_LOGIN_AT = SYSDATE,
                    EXPIRES_AT = :expires_at
                WHERE MR_NO = :mr_no
                  AND DEVICE_INSTALL_ID = :device_install_id
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("device_install_id", deviceInstallId));
            cmd.Parameters.Add(new OracleParameter("expires_at", expiresAt));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<TrustedDeviceRecord>> GetTrustedDevicesAsync(string mrNo)
        {
            var result = new List<TrustedDeviceRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT
                    TRUSTED_DEVICE_ID,
                    MR_NO,
                    DEVICE_INSTALL_ID,
                    DEVICE_LABEL,
                    PLATFORM,
                    TRUSTED_AT,
                    LAST_LOGIN_AT,
                    EXPIRES_AT,
                    IS_ACTIVE
                FROM PATIENT_TRUSTED_DEVICE
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'
                  AND EXPIRES_AT >= SYSDATE
                ORDER BY NVL(LAST_LOGIN_AT, TRUSTED_AT) DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TrustedDeviceRecord
                {
                    TrustedDeviceId = Convert.ToInt32(reader["TRUSTED_DEVICE_ID"]),
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    DeviceInstallId = reader["DEVICE_INSTALL_ID"]?.ToString() ?? string.Empty,
                    DeviceLabel = reader["DEVICE_LABEL"]?.ToString(),
                    Platform = reader["PLATFORM"]?.ToString(),
                    TrustedAt = Convert.ToDateTime(reader["TRUSTED_AT"]),
                    LastLoginAt = reader["LAST_LOGIN_AT"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["LAST_LOGIN_AT"]),
                    ExpiresAt = Convert.ToDateTime(reader["EXPIRES_AT"]),
                    IsActive = string.Equals(
                        reader["IS_ACTIVE"]?.ToString(),
                        "Y",
                        StringComparison.OrdinalIgnoreCase),
                });
            }

            return result;
        }

        public async Task<bool> RevokeTrustedDeviceAsync(string mrNo, int trustedDeviceId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_TRUSTED_DEVICE
                SET IS_ACTIVE = 'N'
                WHERE MR_NO = :mr_no
                  AND TRUSTED_DEVICE_ID = :trusted_device_id
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("trusted_device_id", trustedDeviceId));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task RevokeAllTrustedDevicesAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_TRUSTED_DEVICE
                SET IS_ACTIVE = 'N'
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
