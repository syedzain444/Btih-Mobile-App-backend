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
        Task EnsurePromotionSchemaAsync(CancellationToken cancellationToken = default);
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
            "PATIENT_APP_PIN",
            "GUEST_PATIENT",
            "GUEST_APPOINTMENT",
            "PATIENT_PROFILE_PHOTO",
            "MOBILE_PAYMENT_INTENT",
            "MOBILE_PAYMENT_TRANSACTION",
            "MOBILE_ADMIN_USER",
            "MOBILE_AUDIT_LOG",
            "TELEMED_SESSION",
            "MOBILE_CONTENT_LOCALIZED",
            "MOBILE_PROMOTION",
            "MOBILE_FAQ",
            "MOBILE_SUPPORT_TICKET",
            "MOBILE_APP_SESSION",
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

        /// <summary>
        /// Creates MOBILE_PROMOTION table/sequence if missing (required for admin panel promotions).
        /// </summary>
        public async Task EnsurePromotionSchemaAsync(CancellationToken cancellationToken = default)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                return;
            }

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            if (await TableExistsAsync(conn, "MOBILE_PROMOTION", cancellationToken))
            {
                return;
            }

            if (!await SequenceExistsAsync(conn, "MOBILE_PROMOTION_SEQ", cancellationToken))
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE SEQUENCE MOBILE_PROMOTION_SEQ
                        START WITH 1
                        INCREMENT BY 1
                        NOCACHE
                        NOCYCLE", cancellationToken);
            }

            await ExecuteDdlAsync(conn, @"
                CREATE TABLE MOBILE_PROMOTION (
                    PROMOTION_ID      NUMBER         NOT NULL,
                    TITLE             VARCHAR2(120)  NOT NULL,
                    IMAGE_URL         VARCHAR2(500)  NOT NULL,
                    SORT_ORDER        NUMBER         DEFAULT 0 NOT NULL,
                    DURATION_SECONDS  NUMBER         DEFAULT 5 NOT NULL,
                    IS_ACTIVE         CHAR(1)        DEFAULT 'Y' NOT NULL,
                    START_AT          DATE,
                    END_AT            DATE,
                    CREATED_AT        DATE           DEFAULT SYSDATE NOT NULL,
                    UPDATED_AT        DATE           DEFAULT SYSDATE NOT NULL,
                    CONSTRAINT PK_MOBILE_PROMOTION PRIMARY KEY (PROMOTION_ID),
                    CONSTRAINT CHK_MOBILE_PROMO_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N'))
                )", cancellationToken);

            await ExecuteDdlAsync(conn, @"
                CREATE OR REPLACE TRIGGER TRG_MOBILE_PROMOTION_BI
                BEFORE INSERT ON MOBILE_PROMOTION
                FOR EACH ROW
                BEGIN
                    IF :NEW.PROMOTION_ID IS NULL THEN
                        SELECT MOBILE_PROMOTION_SEQ.NEXTVAL
                          INTO :NEW.PROMOTION_ID
                          FROM DUAL;
                    END IF;
                END;", cancellationToken);

            try
            {
                await ExecuteDdlAsync(conn, @"
                    CREATE INDEX IDX_MOBILE_PROMO_ACTIVE
                        ON MOBILE_PROMOTION (IS_ACTIVE, SORT_ORDER, START_AT, END_AT)", cancellationToken);
            }
            catch (OracleException ex) when (ex.Number == 955)
            {
                // Index already exists.
            }
        }

        private static async Task<bool> TableExistsAsync(
            OracleConnection conn,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM USER_TABLES
                WHERE TABLE_NAME = :table_name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("table_name", tableName));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static async Task<bool> SequenceExistsAsync(
            OracleConnection conn,
            string sequenceName,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM USER_SEQUENCES
                WHERE SEQUENCE_NAME = :sequence_name", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("sequence_name", sequenceName));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static async Task ExecuteDdlAsync(
            OracleConnection conn,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var cmd = new OracleCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
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
