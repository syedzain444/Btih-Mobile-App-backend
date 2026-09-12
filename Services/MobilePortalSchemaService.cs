using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Services
{
    public interface IMobilePortalSchemaService
    {
        Task<IReadOnlyList<string>> GetMissingTablesAsync();
    }

    public class MobilePortalSchemaService : IMobilePortalSchemaService
    {
        private static readonly string[] RequiredTables =
        {
            "PATIENT_MSG_THREAD",
            "PATIENT_MSG",
            "PATIENT_MSG_ATTACHMENT",
            "MOBILE_PATIENT_REGISTRATION",
            "PATIENT_MED_REFILL_REQUEST",
            "PATIENT_MED_REMINDER",
            "PATIENT_DEVICE_TOKEN",
            "PATIENT_NOTIFICATION",
            "PATIENT_TRUSTED_DEVICE",
            "GUEST_PATIENT",
        };

        private readonly IConfiguration _configuration;

        public MobilePortalSchemaService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<string>> GetMissingTablesAsync()
        {
            var missing = new List<string>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            if (string.IsNullOrWhiteSpace(connStr))
            {
                return RequiredTables;
            }

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            foreach (var table in RequiredTables)
            {
                await using var cmd = new OracleCommand(@"
                    SELECT COUNT(*)
                    FROM USER_TABLES
                    WHERE TABLE_NAME = :table_name", conn);

                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter("table_name", table));

                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    missing.Add(table);
                }
            }

            return missing;
        }
    }
}
