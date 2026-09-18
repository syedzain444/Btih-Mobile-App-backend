using System.Diagnostics;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Services
{
    public interface IMobilePortalSchemaService
    {
        IReadOnlyList<string> RequiredTables { get; }
        Task<IReadOnlyList<string>> GetMissingTablesAsync();
        Task<IReadOnlyList<ConnectedDatabaseInfo>> GetConnectedDatabasesAsync();
    }

    public sealed class ConnectedDatabaseInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public bool Configured { get; init; }
        public bool Connected { get; init; }
        public string Status { get; init; } = "unknown";
        public int? LatencyMs { get; init; }
        public string? Error { get; init; }

        public string? UserId { get; init; }
        public string? Host { get; init; }
        public int? Port { get; init; }
        public string? ServiceName { get; init; }
        public string? DataSource { get; init; }

        public string? SessionUser { get; init; }
        public string? CurrentSchema { get; init; }
        public string? DbName { get; init; }
        public string? InstanceName { get; init; }
        public string? ServerHost { get; init; }
        public string? ServerServiceName { get; init; }
        public string? ServerTime { get; init; }
        public string? OracleBanner { get; init; }
    }

    public class MobilePortalSchemaService : IMobilePortalSchemaService
    {
        private static readonly string[] RequiredTablesList =
        {
            "PATIENT_MSG_THREAD",
            "PATIENT_MSG",
            "PATIENT_MSG_ATTACHMENT",
            "MOBILE_PATIENT_REGISTRATION",
            "PATIENT_MED_REFILL_REQUEST",
            "PATIENT_MED_REMINDER",
            "PATIENT_DEVICE_TOKEN",
            "PATIENT_NOTIFICATION",
            "PATIENT_RECENT_ACTIVITY",
            "PATIENT_TRUSTED_DEVICE",
            "GUEST_PATIENT",
            "PATIENT_PROFILE_PHOTO",
        };

        public IReadOnlyList<string> RequiredTables => RequiredTablesList;

        private static readonly (string Name, string Purpose)[] ConnectionTargets =
        {
            ("HMISConnection", "Primary patient / mobile portal schema"),
            ("HOS_WEB_MVC_LIVE", "Doctors, schedules, and related hospital web data"),
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
                return RequiredTablesList;
            }

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            foreach (var table in RequiredTablesList)
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

        public async Task<IReadOnlyList<ConnectedDatabaseInfo>> GetConnectedDatabasesAsync()
        {
            var results = new List<ConnectedDatabaseInfo>(ConnectionTargets.Length);
            foreach (var (name, purpose) in ConnectionTargets)
            {
                results.Add(await ProbeConnectionAsync(name, purpose));
            }

            return results;
        }

        private async Task<ConnectedDatabaseInfo> ProbeConnectionAsync(
            string connectionName,
            string purpose)
        {
            var raw = _configuration.GetConnectionString(connectionName);
            var parsed = ParseOracleConnection(raw);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new ConnectedDatabaseInfo
                {
                    Name = connectionName,
                    Purpose = purpose,
                    Configured = false,
                    Connected = false,
                    Status = "not_configured",
                    Error = $"ConnectionStrings:{connectionName} is empty.",
                };
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await using var conn = new OracleConnection(raw);
                await conn.OpenAsync();

                string? sessionUser = null;
                string? currentSchema = null;
                string? dbName = null;
                string? instanceName = null;
                string? serverHost = null;
                string? serverServiceName = null;
                string? serverTime = null;
                string? banner = null;

                await using (var cmd = new OracleCommand(@"
                    SELECT
                        SYS_CONTEXT('USERENV', 'SESSION_USER') AS SESSION_USER,
                        SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AS CURRENT_SCHEMA,
                        SYS_CONTEXT('USERENV', 'DB_NAME') AS DB_NAME,
                        SYS_CONTEXT('USERENV', 'INSTANCE_NAME') AS INSTANCE_NAME,
                        SYS_CONTEXT('USERENV', 'SERVER_HOST') AS SERVER_HOST,
                        SYS_CONTEXT('USERENV', 'SERVICE_NAME') AS SERVICE_NAME,
                        TO_CHAR(SYSTIMESTAMP, 'YYYY-MM-DD""T""HH24:MI:SS.FF3TZH:TZM') AS SERVER_TIME
                    FROM DUAL", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        sessionUser = reader["SESSION_USER"]?.ToString();
                        currentSchema = reader["CURRENT_SCHEMA"]?.ToString();
                        dbName = reader["DB_NAME"]?.ToString();
                        instanceName = reader["INSTANCE_NAME"]?.ToString();
                        serverHost = reader["SERVER_HOST"]?.ToString();
                        serverServiceName = reader["SERVICE_NAME"]?.ToString();
                        serverTime = reader["SERVER_TIME"]?.ToString();
                    }
                }

                try
                {
                    await using var bannerCmd = new OracleCommand(@"
                        SELECT BANNER
                        FROM V$VERSION
                        WHERE BANNER LIKE 'Oracle%'
                          AND ROWNUM = 1", conn);
                    banner = (await bannerCmd.ExecuteScalarAsync())?.ToString();
                }
                catch
                {
                    // V$VERSION may be restricted; ignore.
                }

                sw.Stop();
                return new ConnectedDatabaseInfo
                {
                    Name = connectionName,
                    Purpose = purpose,
                    Configured = true,
                    Connected = true,
                    Status = "connected",
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    UserId = parsed.UserId,
                    Host = parsed.Host,
                    Port = parsed.Port,
                    ServiceName = parsed.ServiceName,
                    DataSource = parsed.DataSourceSummary,
                    SessionUser = sessionUser,
                    CurrentSchema = currentSchema,
                    DbName = dbName,
                    InstanceName = instanceName,
                    ServerHost = serverHost,
                    ServerServiceName = serverServiceName,
                    ServerTime = serverTime,
                    OracleBanner = banner,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ConnectedDatabaseInfo
                {
                    Name = connectionName,
                    Purpose = purpose,
                    Configured = true,
                    Connected = false,
                    Status = "unreachable",
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    Error = ex.Message,
                    UserId = parsed.UserId,
                    Host = parsed.Host,
                    Port = parsed.Port,
                    ServiceName = parsed.ServiceName,
                    DataSource = parsed.DataSourceSummary,
                };
            }
        }

        private static (
            string? UserId,
            string? Host,
            int? Port,
            string? ServiceName,
            string? DataSourceSummary
        ) ParseOracleConnection(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return (null, null, null, null, null);
            }

            string? userId = null;
            string? dataSource = null;

            try
            {
                var builder = new OracleConnectionStringBuilder(connectionString);
                userId = string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID;
                dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? null : builder.DataSource;
            }
            catch
            {
                // Fall through to regex parsing.
            }

            var source = dataSource ?? connectionString;
            var host = MatchFirst(source, @"\(HOST\s*=\s*([^)\s]+)\)");
            var portRaw = MatchFirst(source, @"\(PORT\s*=\s*([^)\s]+)\)");
            var serviceName =
                MatchFirst(source, @"\(SERVICE_NAME\s*=\s*([^)\s]+)\)") ??
                MatchFirst(source, @"\(SID\s*=\s*([^)\s]+)\)");

            int? port = null;
            if (int.TryParse(portRaw, out var parsedPort))
            {
                port = parsedPort;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = MatchFirst(connectionString, @"User\s*Id\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
            }

            string? summary = null;
            if (!string.IsNullOrWhiteSpace(host) || !string.IsNullOrWhiteSpace(serviceName))
            {
                summary = $"{host ?? "?"}:{port?.ToString() ?? "?"}/{serviceName ?? "?"}";
            }
            else if (!string.IsNullOrWhiteSpace(dataSource) && dataSource.Length <= 120)
            {
                summary = dataSource;
            }

            return (userId?.Trim(), host, port, serviceName, summary);
        }

        private static string? MatchFirst(
            string input,
            string pattern,
            RegexOptions options = RegexOptions.IgnoreCase)
        {
            var match = Regex.Match(input, pattern, options);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}
