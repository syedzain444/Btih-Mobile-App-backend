using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IAnalyticsRepository
    {
        Task<UserAnalyticsDto> GetUserStatisticsAsync(DateTime from, DateTime to);
        Task<VisitAnalyticsDto> GetVisitAnalyticsAsync(DateTime from, DateTime to);
        Task<EngagementAnalyticsDto> GetEngagementMetricsAsync(DateTime from, DateTime to);
        Task<AppSessionStartResult> StartSessionAsync(string mrNo, StartAppSessionRequest request);
        Task<bool> EndSessionAsync(string mrNo, EndAppSessionRequest request);
    }

    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly IConfiguration _configuration;
        private bool? _sessionTableAvailable;

        public AnalyticsRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<UserAnalyticsDto> GetUserStatisticsAsync(DateTime from, DateTime to)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var hasSessions = await HasSessionTableAsync(conn);

            var totalUsers = await ScalarIntAsync(conn, @"
                SELECT COUNT(*) FROM (
                    SELECT MR_NO FROM MOBILE_PATIENT_REGISTRATION WHERE IS_ACTIVE = 'Y'
                    UNION
                    SELECT MR_NO FROM PATIENT_MST
                     WHERE PATIENT_PASSWORD IS NOT NULL
                       AND TRIM(PATIENT_PASSWORD) IS NOT NULL
                )");

            var mobileRegistrations = await ScalarIntAsync(conn, @"
                SELECT COUNT(*)
                  FROM MOBILE_PATIENT_REGISTRATION
                 WHERE IS_ACTIVE = 'Y'");

            var hmisPortalUsers = await ScalarIntAsync(conn, @"
                SELECT COUNT(*)
                  FROM PATIENT_MST
                 WHERE PATIENT_PASSWORD IS NOT NULL
                   AND TRIM(PATIENT_PASSWORD) IS NOT NULL");

            var activeSql = BuildActiveUsersSql(hasSessions);
            var activeUsers = await ScalarIntAsync(conn, activeSql, from, to);

            var newUsers = await ScalarIntAsync(conn, @"
                SELECT COUNT(*)
                  FROM MOBILE_PATIENT_REGISTRATION
                 WHERE IS_ACTIVE = 'Y'
                   AND CREATED_AT >= :from_date
                   AND CREATED_AT <= :to_date", from, to);

            var returningSql = BuildReturningUsersSql(hasSessions);
            var returningUsers = await ScalarIntAsync(conn, returningSql, from, to);

            return new UserAnalyticsDto
            {
                Period = new AnalyticsPeriodDto { From = from, To = to },
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                NewUsers = newUsers,
                ReturningUsers = returningUsers,
                MobileRegistrations = mobileRegistrations,
                HmisPortalUsers = hmisPortalUsers,
            };
        }

        public async Task<VisitAnalyticsDto> GetVisitAnalyticsAsync(DateTime from, DateTime to)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var hasSessions = await HasSessionTableAsync(conn);
            var visitSourceSql = BuildVisitEventsSql(hasSessions);

            var daily = await ReadVisitBucketsAsync(
                conn,
                visitSourceSql,
                "TRUNC(event_at)",
                from,
                to);

            var weekly = await ReadVisitBucketsAsync(
                conn,
                visitSourceSql,
                "TRUNC(event_at, 'IW')",
                from,
                to);

            var monthly = await ReadVisitBucketsAsync(
                conn,
                visitSourceSql,
                "TRUNC(event_at, 'MM')",
                from,
                to);

            var totalVisits = daily.Sum(x => x.Visits);
            var uniqueUsers = await ScalarIntAsync(conn, $@"
                SELECT COUNT(DISTINCT mr_no)
                  FROM ({visitSourceSql})
                 WHERE event_at >= :from_date
                   AND event_at <= :to_date", from, to);

            return new VisitAnalyticsDto
            {
                Period = new AnalyticsPeriodDto { From = from, To = to },
                TotalVisits = totalVisits,
                UniqueUsers = uniqueUsers,
                Daily = daily,
                Weekly = weekly,
                Monthly = monthly,
            };
        }

        public async Task<EngagementAnalyticsDto> GetEngagementMetricsAsync(DateTime from, DateTime to)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var hasSessions = await HasSessionTableAsync(conn);
            if (!hasSessions)
            {
                return BuildEngagementFallback(from, to);
            }

            var totals = await ReadEngagementTotalsAsync(conn, from, to);
            var topUsers = await ReadTopUsersByTimeAsync(conn, from, to, 10);

            return new EngagementAnalyticsDto
            {
                Period = new AnalyticsPeriodDto { From = from, To = to },
                TotalSessions = totals.TotalSessions,
                CompletedSessions = totals.CompletedSessions,
                AvgSessionDurationSeconds = totals.AvgSeconds,
                MedianSessionDurationSeconds = totals.MedianSeconds,
                TotalTimeSpentSeconds = totals.TotalSeconds,
                TopUsersByTime = topUsers,
                SessionTrackingAvailable = true,
            };
        }

        public async Task<AppSessionStartResult> StartSessionAsync(string mrNo, StartAppSessionRequest request)
        {
            var sessionGuid = string.IsNullOrWhiteSpace(request.SessionGuid)
                ? Guid.NewGuid().ToString("D")
                : request.SessionGuid.Trim();

            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            if (!await HasSessionTableAsync(conn))
            {
                return new AppSessionStartResult
                {
                    SessionGuid = sessionGuid,
                    StartedAt = DateTime.Now,
                };
            }

            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_APP_SESSION (
                    SESSION_GUID, MR_NO, PLATFORM, APP_VERSION, STARTED_AT, IS_ACTIVE
                ) VALUES (
                    :session_guid, :mr_no, :platform, :app_version, SYSDATE, 'Y'
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("session_guid", sessionGuid));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter(
                "platform",
                string.IsNullOrWhiteSpace(request.Platform) ? (object)DBNull.Value : request.Platform.Trim()));
            cmd.Parameters.Add(new OracleParameter(
                "app_version",
                string.IsNullOrWhiteSpace(request.AppVersion) ? (object)DBNull.Value : request.AppVersion.Trim()));

            await cmd.ExecuteNonQueryAsync();

            return new AppSessionStartResult
            {
                SessionGuid = sessionGuid,
                StartedAt = DateTime.Now,
            };
        }

        public async Task<bool> EndSessionAsync(string mrNo, EndAppSessionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionGuid))
            {
                return false;
            }

            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            if (!await HasSessionTableAsync(conn))
            {
                return false;
            }

            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_APP_SESSION
                   SET ENDED_AT = SYSDATE,
                       DURATION_SECONDS = NVL(
                           :duration_seconds,
                           ROUND((SYSDATE - STARTED_AT) * 86400)
                       ),
                       IS_ACTIVE = 'N'
                 WHERE SESSION_GUID = :session_guid
                   AND MR_NO = :mr_no
                   AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(
                "duration_seconds",
                request.DurationSeconds.HasValue ? request.DurationSeconds.Value : (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("session_guid", request.SessionGuid.Trim()));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static EngagementAnalyticsDto BuildEngagementFallback(DateTime from, DateTime to)
        {
            return new EngagementAnalyticsDto
            {
                Period = new AnalyticsPeriodDto { From = from, To = to },
                SessionTrackingAvailable = false,
            };
        }

        private async Task<bool> HasSessionTableAsync(OracleConnection conn)
        {
            if (_sessionTableAvailable.HasValue)
            {
                return _sessionTableAvailable.Value;
            }

            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                  FROM USER_TABLES
                 WHERE TABLE_NAME = 'MOBILE_APP_SESSION'", conn);

            _sessionTableAvailable = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            return _sessionTableAvailable.Value;
        }

        private static string BuildActiveUsersSql(bool hasSessions)
        {
            var parts = new List<string>
            {
                @"SELECT DISTINCT MR_NO
                    FROM PATIENT_RECENT_ACTIVITY
                   WHERE VIEWED_AT >= :from_date
                     AND VIEWED_AT <= :to_date",
                @"SELECT DISTINCT MR_NO
                    FROM PATIENT_DEVICE_TOKEN
                   WHERE IS_ACTIVE = 'Y'
                     AND UPDATED_AT >= :from_date
                     AND UPDATED_AT <= :to_date",
            };

            if (hasSessions)
            {
                parts.Add(@"SELECT DISTINCT MR_NO
                              FROM MOBILE_APP_SESSION
                             WHERE STARTED_AT >= :from_date
                               AND STARTED_AT <= :to_date");
            }

            return $@"
                SELECT COUNT(*) FROM (
                    {string.Join(" UNION ", parts)}
                )";
        }

        private static string BuildReturningUsersSql(bool hasSessions)
        {
            var activeParts = new List<string>
            {
                @"SELECT DISTINCT MR_NO
                    FROM PATIENT_RECENT_ACTIVITY
                   WHERE VIEWED_AT >= :from_date
                     AND VIEWED_AT <= :to_date",
            };

            if (hasSessions)
            {
                activeParts.Add(@"SELECT DISTINCT MR_NO
                                    FROM MOBILE_APP_SESSION
                                   WHERE STARTED_AT >= :from_date
                                     AND STARTED_AT <= :to_date");
            }

            var priorParts = new List<string>
            {
                @"SELECT 1
                    FROM PATIENT_RECENT_ACTIVITY p
                   WHERE p.MR_NO = a.MR_NO
                     AND p.VIEWED_AT < :from_date",
            };

            if (hasSessions)
            {
                priorParts.Add(@"SELECT 1
                                   FROM MOBILE_APP_SESSION s
                                  WHERE s.MR_NO = a.MR_NO
                                    AND s.STARTED_AT < :from_date");
            }

            var priorExists = string.Join(" OR ", priorParts.Select(p => $"EXISTS ({p})"));

            return $@"
                SELECT COUNT(*) FROM (
                    SELECT DISTINCT MR_NO
                      FROM (
                        {string.Join(" UNION ", activeParts)}
                      ) a
                     WHERE {priorExists}
                )";
        }

        private static string BuildVisitEventsSql(bool hasSessions)
        {
            var parts = new List<string>
            {
                @"SELECT MR_NO, VIEWED_AT AS event_at
                    FROM PATIENT_RECENT_ACTIVITY",
            };

            if (hasSessions)
            {
                parts.Add(@"SELECT MR_NO, STARTED_AT AS event_at
                              FROM MOBILE_APP_SESSION");
            }

            return string.Join(" UNION ALL ", parts);
        }

        private static async Task<int> ScalarIntAsync(
            OracleConnection conn,
            string sql,
            DateTime? from = null,
            DateTime? to = null)
        {
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            if (from.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("from_date", from.Value));
            }

            if (to.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("to_date", to.Value));
            }

            var value = await cmd.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static async Task<List<VisitBucketDto>> ReadVisitBucketsAsync(
            OracleConnection conn,
            string visitSourceSql,
            string groupExpression,
            DateTime from,
            DateTime to)
        {
            var buckets = new List<VisitBucketDto>();

            await using var cmd = new OracleCommand($@"
                SELECT period_start,
                       visit_count,
                       unique_users
                  FROM (
                    SELECT {groupExpression} AS period_start,
                           COUNT(*) AS visit_count,
                           COUNT(DISTINCT mr_no) AS unique_users
                      FROM ({visitSourceSql})
                     WHERE event_at >= :from_date
                       AND event_at <= :to_date
                     GROUP BY {groupExpression}
                  )
                 ORDER BY period_start", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("from_date", from));
            cmd.Parameters.Add(new OracleParameter("to_date", to));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var periodStart = Convert.ToDateTime(reader["period_start"]);
                buckets.Add(new VisitBucketDto
                {
                    PeriodStart = periodStart,
                    Label = FormatBucketLabel(periodStart, groupExpression),
                    Visits = Convert.ToInt32(reader["visit_count"]),
                    UniqueUsers = Convert.ToInt32(reader["unique_users"]),
                });
            }

            return buckets;
        }

        private static string FormatBucketLabel(DateTime periodStart, string groupExpression)
        {
            if (groupExpression.Contains("'MM'", StringComparison.Ordinal))
            {
                return periodStart.ToString("yyyy-MM");
            }

            if (groupExpression.Contains("'IW'", StringComparison.Ordinal))
            {
                return periodStart.ToString("yyyy-MM-dd");
            }

            return periodStart.ToString("yyyy-MM-dd");
        }

        private static async Task<(int TotalSessions, int CompletedSessions, int AvgSeconds, int MedianSeconds, long TotalSeconds)>
            ReadEngagementTotalsAsync(OracleConnection conn, DateTime from, DateTime to)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*) AS total_sessions,
                       SUM(CASE WHEN DURATION_SECONDS IS NOT NULL THEN 1 ELSE 0 END) AS completed_sessions,
                       NVL(AVG(DURATION_SECONDS), 0) AS avg_seconds,
                       NVL(MEDIAN(DURATION_SECONDS), 0) AS median_seconds,
                       NVL(SUM(DURATION_SECONDS), 0) AS total_seconds
                  FROM MOBILE_APP_SESSION
                 WHERE STARTED_AT >= :from_date
                   AND STARTED_AT <= :to_date", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("from_date", from));
            cmd.Parameters.Add(new OracleParameter("to_date", to));

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return (0, 0, 0, 0, 0);
            }

            return (
                Convert.ToInt32(reader["total_sessions"]),
                Convert.ToInt32(reader["completed_sessions"]),
                Convert.ToInt32(Math.Round(Convert.ToDecimal(reader["avg_seconds"]))),
                Convert.ToInt32(Math.Round(Convert.ToDecimal(reader["median_seconds"]))),
                Convert.ToInt64(reader["total_seconds"]));
        }

        private static async Task<List<EngagementUserDto>> ReadTopUsersByTimeAsync(
            OracleConnection conn,
            DateTime from,
            DateTime to,
            int take)
        {
            var users = new List<EngagementUserDto>();

            await using var cmd = new OracleCommand($@"
                SELECT * FROM (
                    SELECT MR_NO,
                           COUNT(*) AS session_count,
                           NVL(SUM(DURATION_SECONDS), 0) AS total_seconds
                      FROM MOBILE_APP_SESSION
                     WHERE STARTED_AT >= :from_date
                       AND STARTED_AT <= :to_date
                     GROUP BY MR_NO
                     ORDER BY total_seconds DESC, session_count DESC
                )
                WHERE ROWNUM <= :take", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("from_date", from));
            cmd.Parameters.Add(new OracleParameter("to_date", to));
            cmd.Parameters.Add(new OracleParameter("take", take));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new EngagementUserDto
                {
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    SessionCount = Convert.ToInt32(reader["session_count"]),
                    TotalSeconds = Convert.ToInt32(reader["total_seconds"]),
                });
            }

            return users;
        }
    }
}
