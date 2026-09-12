using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class PushNotificationRepository : IPushNotificationRepository
    {
        private readonly IConfiguration _configuration;

        public PushNotificationRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task RegisterDeviceTokenAsync(RegisterDeviceTokenRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                MERGE INTO PATIENT_DEVICE_TOKEN t
                USING (
                    SELECT :mr_no AS MR_NO, :device_token AS DEVICE_TOKEN FROM dual
                ) s
                ON (t.MR_NO = s.MR_NO AND t.DEVICE_TOKEN = s.DEVICE_TOKEN)
                WHEN MATCHED THEN
                    UPDATE SET
                        PLATFORM = :platform,
                        IS_ACTIVE = 'Y',
                        UPDATED_AT = SYSDATE
                WHEN NOT MATCHED THEN
                    INSERT (MR_NO, DEVICE_TOKEN, PLATFORM, IS_ACTIVE, CREATED_AT, UPDATED_AT)
                    VALUES (:mr_no, :device_token, :platform, 'Y', SYSDATE, SYSDATE)", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));
            cmd.Parameters.Add(new OracleParameter("device_token", request.DeviceToken));
            cmd.Parameters.Add(new OracleParameter("platform", request.Platform));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UnregisterDeviceTokenAsync(UnregisterDeviceTokenRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_DEVICE_TOKEN
                SET IS_ACTIVE = 'N',
                    UPDATED_AT = SYSDATE
                WHERE MR_NO = :mr_no
                  AND DEVICE_TOKEN = :device_token", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));
            cmd.Parameters.Add(new OracleParameter("device_token", request.DeviceToken));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<PatientDeviceToken>> GetActiveTokensAsync(string mrNo)
        {
            var tokens = new List<PatientDeviceToken>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT MR_NO, DEVICE_TOKEN, PLATFORM, UPDATED_AT
                FROM PATIENT_DEVICE_TOKEN
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                DateTime? updatedAt = null;
                if (reader["UPDATED_AT"] is DateTime dt)
                {
                    updatedAt = dt;
                }

                tokens.Add(new PatientDeviceToken
                {
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    DeviceToken = reader["DEVICE_TOKEN"]?.ToString() ?? string.Empty,
                    Platform = reader["PLATFORM"]?.ToString() ?? string.Empty,
                    UpdatedAt = updatedAt,
                });
            }

            return tokens;
        }

        public async Task UnregisterAllTokensAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_DEVICE_TOKEN
                SET IS_ACTIVE = 'N',
                    UPDATED_AT = SYSDATE
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeactivateTokenAsync(string mrNo, string deviceToken)
        {
            await UnregisterDeviceTokenAsync(new UnregisterDeviceTokenRequest
            {
                MrNo = mrNo,
                DeviceToken = deviceToken,
            });
        }
    }
}
