using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;
using System.Text;

namespace HospitalMobileAPPApi.Repository
{
    public interface IAdminPortalRepository
    {
        Task<(List<AdminPortalUserDto> Items, int Total)> GetPortalUsersAsync(string? search, int skip, int take);
        Task<(List<PatientAppointment> Items, int Total)> GetAppointmentsAsync(
            string? status,
            DateTime? from,
            DateTime? to,
            string? search,
            int skip,
            int take);
        Task<PatientAppointment?> GetAppointmentAsync(string appointmentId);
        Task<bool> UpdateAppointmentStatusAsync(string appointmentId, string status, string? note);
        Task<RegistrationReportDto> GetRegistrationReportAsync(DateTime from, DateTime to);
        Task<AppointmentReportDto> GetAppointmentReportAsync(string? status, DateTime from, DateTime to);
        Task<byte[]> ExportEngagementCsvAsync(DateTime from, DateTime to);
    }

    public class AdminPortalRepository : IAdminPortalRepository
    {
        private readonly IConfiguration _configuration;
        private bool? _sessionTableAvailable;

        public AdminPortalRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<(List<AdminPortalUserDto> Items, int Total)> GetPortalUsersAsync(
            string? search,
            int skip,
            int take)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var whereClause = BuildUserSearchClause(search);
            var total = 0;
            await using (var countCmd = new OracleCommand($@"
                SELECT COUNT(*)
                  FROM (
                    SELECT MR_NO, FIRST_NAME, LAST_NAME, CONTACT_NO
                      FROM MOBILE_PATIENT_REGISTRATION
                     WHERE IS_ACTIVE = 'Y'
                    UNION
                    SELECT pm.MR_NO, pm.FIRST_NAME, pm.LAST_NAME, pi.CONTACT_NO
                      FROM PATIENT_MST pm
                      JOIN PATIENT_INFORMATION pi ON pi.MR_NO = pm.MR_NO
                     WHERE pm.PATIENT_PASSWORD IS NOT NULL
                       AND TRIM(pm.PATIENT_PASSWORD) IS NOT NULL
                  ) u
                 {whereClause}", conn))
            {
                countCmd.BindByName = true;
                BindSearch(countCmd, search);
                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var items = new List<AdminPortalUserDto>();
            await using var cmd = new OracleCommand($@"
                SELECT * FROM (
                    SELECT inner_q.*, ROWNUM rn FROM (
                        SELECT MR_NO, FIRST_NAME, LAST_NAME, CONTACT_NO, EMAIL,
                               SOURCE, PROFILE_SETUP_COMPLETE, REGISTERED_AT
                          FROM (
                            SELECT MR_NO, FIRST_NAME, LAST_NAME, CONTACT_NO, EMAIL_ADDRESS AS EMAIL,
                                   'MOBILE' AS SOURCE,
                                   CASE WHEN PROFILE_SETUP_COMPLETE = 'Y' THEN 1 ELSE 0 END AS PROFILE_SETUP_COMPLETE,
                                   CREATED_AT AS REGISTERED_AT
                              FROM MOBILE_PATIENT_REGISTRATION
                             WHERE IS_ACTIVE = 'Y'
                            UNION
                            SELECT pm.MR_NO, pm.FIRST_NAME, pm.LAST_NAME, pi.CONTACT_NO, pi.EMAIL_ADDRESS,
                                   'HMIS', 1, NULL
                              FROM PATIENT_MST pm
                              JOIN PATIENT_INFORMATION pi ON pi.MR_NO = pm.MR_NO
                             WHERE pm.PATIENT_PASSWORD IS NOT NULL
                               AND TRIM(pm.PATIENT_PASSWORD) IS NOT NULL
                          ) u
                         {whereClause}
                         ORDER BY REGISTERED_AT DESC NULLS LAST, MR_NO
                    ) inner_q WHERE ROWNUM <= :max_row
                ) WHERE rn > :skip", conn);

            cmd.BindByName = true;
            BindSearch(cmd, search);
            cmd.Parameters.Add(new OracleParameter("max_row", skip + take));
            cmd.Parameters.Add(new OracleParameter("skip", skip));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new AdminPortalUserDto
                {
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    FirstName = reader["FIRST_NAME"]?.ToString(),
                    LastName = reader["LAST_NAME"]?.ToString(),
                    ContactNo = reader["CONTACT_NO"]?.ToString(),
                    Email = reader["EMAIL"]?.ToString(),
                    Source = reader["SOURCE"]?.ToString() ?? string.Empty,
                    ProfileSetupComplete = Convert.ToInt32(reader["PROFILE_SETUP_COMPLETE"]) == 1,
                    RegisteredAt = reader["REGISTERED_AT"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["REGISTERED_AT"]),
                });
            }

            return (items, total);
        }

        public async Task<(List<PatientAppointment> Items, int Total)> GetAppointmentsAsync(
            string? status,
            DateTime? from,
            DateTime? to,
            string? search,
            int skip,
            int take)
        {
            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var filters = BuildAppointmentFilters(status, from, to, search);
            var total = await ScalarIntAsync(conn, $@"
                SELECT COUNT(*)
                  FROM APPOINTMENT a
                 WHERE NVL(a.IS_ACTIVE, 'Y') = 'Y'
                   {filters.Sql}", filters);

            var items = new List<PatientAppointment>();
            await using var cmd = new OracleCommand($@"
                SELECT * FROM (
                    SELECT inner_q.*, ROWNUM rn FROM (
                        SELECT a.APPOINTMENT_ID, a.NAME, a.PHONE, a.MRNUM, a.EMAIL, a.WEEK_ID,
                               a.APPOINTMENTTIME, a.STATUS, a.DOCTOR_ID, a.DEPARTMENT_ID,
                               a.PURPOSE, a.CREATED_AT, d.DOCTOR_NAME
                          FROM APPOINTMENT a
                          JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                         WHERE NVL(a.IS_ACTIVE, 'Y') = 'Y'
                           {filters.Sql}
                         ORDER BY a.CREATED_AT DESC
                    ) inner_q WHERE ROWNUM <= :max_row
                ) WHERE rn > :skip", conn);

            cmd.BindByName = true;
            BindAppointmentFilters(cmd, filters);
            cmd.Parameters.Add(new OracleParameter("max_row", skip + take));
            cmd.Parameters.Add(new OracleParameter("skip", skip));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapAppointment(reader));
            }

            return (items, total);
        }

        public async Task<PatientAppointment?> GetAppointmentAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId))
            {
                return null;
            }

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT a.APPOINTMENT_ID, a.NAME, a.PHONE, a.MRNUM, a.EMAIL, a.WEEK_ID,
                       a.APPOINTMENTTIME, a.STATUS, a.DOCTOR_ID, a.DEPARTMENT_ID,
                       a.PURPOSE, a.CREATED_AT, d.DOCTOR_NAME
                  FROM APPOINTMENT a
                  JOIN DOCTOR d ON a.DOCTOR_ID = d.DOCTOR_ID
                 WHERE a.APPOINTMENT_ID = :appointment_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("appointment_id", appointmentId.Trim()));
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapAppointment(reader) : null;
        }

        public async Task<bool> UpdateAppointmentStatusAsync(string appointmentId, string status, string? note)
        {
            if (string.IsNullOrWhiteSpace(appointmentId))
            {
                return false;
            }

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            var noteText = string.IsNullOrWhiteSpace(note) ? null : $"[{status.ToUpperInvariant()}: {note.Trim()}]";

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE APPOINTMENT
                   SET STATUS = :status,
                       PURPOSE = CASE
                           WHEN :note IS NULL THEN PURPOSE
                           WHEN PURPOSE IS NULL OR TRIM(PURPOSE) = '' THEN :note
                           ELSE PURPOSE || ' | ' || :note
                       END
                 WHERE APPOINTMENT_ID = :appointment_id
                   AND NVL(IS_ACTIVE, 'Y') = 'Y'
                   AND UPPER(NVL(STATUS, 'PENDING')) NOT IN ('CANCELLED', 'COMPLETED', 'CONFIRMED', 'REJECTED')", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("note", noteText ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("appointment_id", appointmentId.Trim()));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<RegistrationReportDto> GetRegistrationReportAsync(DateTime from, DateTime to)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var mobileTotal = await ScalarIntAsync(conn, @"
                SELECT COUNT(*)
                  FROM MOBILE_PATIENT_REGISTRATION
                 WHERE IS_ACTIVE = 'Y'
                   AND CREATED_AT >= :from_date
                   AND CREATED_AT <= :to_date", from, to);

            var hmisTotal = await ScalarIntAsync(conn, @"
                SELECT COUNT(*)
                  FROM PATIENT_MST
                 WHERE PATIENT_PASSWORD IS NOT NULL
                   AND TRIM(PATIENT_PASSWORD) IS NOT NULL");

            var daily = await ReadRegistrationBucketsAsync(conn, from, to);

            return new RegistrationReportDto
            {
                Period = new AnalyticsPeriodDto { From = from, To = to },
                TotalRegistrations = mobileTotal,
                MobileRegistrations = mobileTotal,
                HmisPortalUsers = hmisTotal,
                Daily = daily,
            };
        }

        public async Task<AppointmentReportDto> GetAppointmentReportAsync(
            string? status,
            DateTime from,
            DateTime to)
        {
            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var filters = BuildAppointmentFilters(status, from, to, null);
            var total = await ScalarIntAsync(conn, $@"
                SELECT COUNT(*)
                  FROM APPOINTMENT a
                 WHERE NVL(a.IS_ACTIVE, 'Y') = 'Y'
                   {filters.Sql}", filters);

            var byStatus = new List<StatusCountDto>();
            await using (var cmd = new OracleCommand($@"
                SELECT NVL(STATUS, 'Pending') AS status_label, COUNT(*) AS status_count
                  FROM APPOINTMENT a
                 WHERE NVL(a.IS_ACTIVE, 'Y') = 'Y'
                   {filters.Sql}
                 GROUP BY NVL(STATUS, 'Pending')
                 ORDER BY status_count DESC", conn))
            {
                cmd.BindByName = true;
                BindAppointmentFilters(cmd, filters);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    byStatus.Add(new StatusCountDto
                    {
                        Status = reader["status_label"]?.ToString() ?? string.Empty,
                        Count = Convert.ToInt32(reader["status_count"]),
                    });
                }
            }

            var daily = new List<ReportBucketDto>();
            await using (var cmd = new OracleCommand($@"
                SELECT TRUNC(a.CREATED_AT) AS period_start, COUNT(*) AS bucket_count
                  FROM APPOINTMENT a
                 WHERE NVL(a.IS_ACTIVE, 'Y') = 'Y'
                   {filters.Sql}
                 GROUP BY TRUNC(a.CREATED_AT)
                 ORDER BY period_start", conn))
            {
                cmd.BindByName = true;
                BindAppointmentFilters(cmd, filters);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var periodStart = Convert.ToDateTime(reader["period_start"]);
                    daily.Add(new ReportBucketDto
                    {
                        PeriodStart = periodStart,
                        Label = periodStart.ToString("yyyy-MM-dd"),
                        Count = Convert.ToInt32(reader["bucket_count"]),
                    });
                }
            }

            return new AppointmentReportDto
            {
                Period = new AnalyticsPeriodDto { From = from, To = to },
                StatusFilter = status,
                TotalAppointments = total,
                ByStatus = byStatus,
                Daily = daily,
            };
        }

        public async Task<byte[]> ExportEngagementCsvAsync(DateTime from, DateTime to)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            if (!await HasSessionTableAsync(conn))
            {
                return Encoding.UTF8.GetBytes("MrNo,SessionGuid,Platform,AppVersion,StartedAt,EndedAt,DurationSeconds\r\n");
            }

            var sb = new StringBuilder();
            sb.AppendLine("MrNo,SessionGuid,Platform,AppVersion,StartedAt,EndedAt,DurationSeconds");

            await using var cmd = new OracleCommand(@"
                SELECT MR_NO, SESSION_GUID, PLATFORM, APP_VERSION, STARTED_AT, ENDED_AT, DURATION_SECONDS
                  FROM MOBILE_APP_SESSION
                 WHERE STARTED_AT >= :from_date
                   AND STARTED_AT <= :to_date
                 ORDER BY STARTED_AT DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("from_date", from));
            cmd.Parameters.Add(new OracleParameter("to_date", to));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sb.Append(Csv(reader["MR_NO"]?.ToString()));
                sb.Append(',');
                sb.Append(Csv(reader["SESSION_GUID"]?.ToString()));
                sb.Append(',');
                sb.Append(Csv(reader["PLATFORM"]?.ToString()));
                sb.Append(',');
                sb.Append(Csv(reader["APP_VERSION"]?.ToString()));
                sb.Append(',');
                sb.Append(Csv(reader["STARTED_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["STARTED_AT"]).ToString("O")));
                sb.Append(',');
                sb.Append(Csv(reader["ENDED_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["ENDED_AT"]).ToString("O")));
                sb.Append(',');
                sb.Append(reader["DURATION_SECONDS"] == DBNull.Value ? string.Empty : reader["DURATION_SECONDS"]?.ToString());
                sb.AppendLine();
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string BuildUserSearchClause(string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return string.Empty;
            }

            return @"WHERE (
                UPPER(MR_NO) LIKE '%' || UPPER(:search) || '%'
                OR UPPER(NVL(FIRST_NAME, '') || ' ' || NVL(LAST_NAME, '')) LIKE '%' || UPPER(:search) || '%'
                OR CONTACT_NO LIKE '%' || :search || '%'
            )";
        }

        private static void BindSearch(OracleCommand cmd, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                cmd.Parameters.Add(new OracleParameter("search", search.Trim()));
            }
        }

        private sealed class AppointmentFilter
        {
            public string Sql { get; init; } = string.Empty;
            public string? Status { get; init; }
            public DateTime? From { get; init; }
            public DateTime? To { get; init; }
            public string? Search { get; init; }
        }

        private static AppointmentFilter BuildAppointmentFilters(
            string? status,
            DateTime? from,
            DateTime? to,
            string? search)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(status))
            {
                parts.Add("AND UPPER(NVL(a.STATUS, 'PENDING')) = UPPER(:status)");
            }

            if (from.HasValue)
            {
                parts.Add("AND a.CREATED_AT >= :from_date");
            }

            if (to.HasValue)
            {
                parts.Add("AND a.CREATED_AT <= :to_date");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                parts.Add(@"AND (
                    UPPER(NVL(a.APPOINTMENT_ID, '')) LIKE '%' || UPPER(:search) || '%'
                    OR UPPER(NVL(a.NAME, '')) LIKE '%' || UPPER(:search) || '%'
                    OR UPPER(NVL(a.MRNUM, '')) LIKE '%' || UPPER(:search) || '%'
                    OR UPPER(NVL(a.PHONE, '')) LIKE '%' || UPPER(:search) || '%'
                    OR UPPER(NVL(a.PURPOSE, '')) LIKE '%' || UPPER(:search) || '%'
                )");
            }

            return new AppointmentFilter
            {
                Sql = string.Join(' ', parts),
                Status = status,
                From = from,
                To = to,
                Search = search,
            };
        }

        private static void BindAppointmentFilters(OracleCommand cmd, AppointmentFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                cmd.Parameters.Add(new OracleParameter("status", filter.Status.Trim()));
            }

            if (filter.From.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("from_date", filter.From.Value));
            }

            if (filter.To.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("to_date", filter.To.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                cmd.Parameters.Add(new OracleParameter("search", filter.Search.Trim()));
            }
        }

        private static async Task<List<ReportBucketDto>> ReadRegistrationBucketsAsync(
            OracleConnection conn,
            DateTime from,
            DateTime to)
        {
            var buckets = new List<ReportBucketDto>();
            await using var cmd = new OracleCommand(@"
                SELECT TRUNC(CREATED_AT) AS period_start, COUNT(*) AS bucket_count
                  FROM MOBILE_PATIENT_REGISTRATION
                 WHERE IS_ACTIVE = 'Y'
                   AND CREATED_AT >= :from_date
                   AND CREATED_AT <= :to_date
                 GROUP BY TRUNC(CREATED_AT)
                 ORDER BY period_start", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("from_date", from));
            cmd.Parameters.Add(new OracleParameter("to_date", to));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var periodStart = Convert.ToDateTime(reader["period_start"]);
                buckets.Add(new ReportBucketDto
                {
                    PeriodStart = periodStart,
                    Label = periodStart.ToString("yyyy-MM-dd"),
                    Count = Convert.ToInt32(reader["bucket_count"]),
                });
            }

            return buckets;
        }

        private static PatientAppointment MapAppointment(OracleDataReader reader)
        {
            return new PatientAppointment
            {
                AppointmentId = reader["APPOINTMENT_ID"]?.ToString(),
                Name = reader["NAME"]?.ToString(),
                PhoneNo = reader["PHONE"]?.ToString(),
                MRNo = reader["MRNUM"]?.ToString(),
                Email = reader["EMAIL"]?.ToString(),
                weekId = reader["WEEK_ID"] != DBNull.Value ? Convert.ToInt32(reader["WEEK_ID"]) : 0,
                AppointmentTime = reader["APPOINTMENTTIME"]?.ToString(),
                Status = reader["STATUS"]?.ToString(),
                DoctorName = reader["DOCTOR_NAME"]?.ToString(),
                DoctorId = reader["DOCTOR_ID"] != DBNull.Value ? Convert.ToInt32(reader["DOCTOR_ID"]) : 0,
                DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["DEPARTMENT_ID"]) : 0,
                purpose = reader["PURPOSE"]?.ToString(),
                CreatedAt = reader["CREATED_AT"] != DBNull.Value
                    ? Convert.ToDateTime(reader["CREATED_AT"])
                    : null,
            };
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

        private static async Task<int> ScalarIntAsync(OracleConnection conn, string sql, AppointmentFilter filter)
        {
            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            BindAppointmentFilters(cmd, filter);
            var value = await cmd.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
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

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
