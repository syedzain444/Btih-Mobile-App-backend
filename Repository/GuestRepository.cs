using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class GuestRepository : IGuestRepository
    {
        private readonly IConfiguration _configuration;

        public GuestRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<GuestProfileRecord?> GetByMobileAsync(string mobileNumber)
        {
            var normalized = NormalizeMobile(mobileNumber);
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT GUEST_ID, FULL_NAME, MOBILE_NUMBER, DATE_OF_BIRTH, GENDER,
                       CREATED_AT, UPDATED_AT
                FROM GUEST_PATIENT
                WHERE MOBILE_NUMBER = :mobile_number
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mobile_number", OracleDbType.Varchar2).Value = normalized;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return MapGuest(reader);
        }

        public async Task<GuestProfileRecord> UpsertProfileAsync(GuestProfileRequest request)
        {
            var normalized = NormalizeMobile(request.MobileNumber);
            var existing = await GetByMobileAsync(normalized);
            if (existing != null)
            {
                await UpdateProfileAsync(existing.GuestId, request, normalized);
                return (await GetByMobileAsync(normalized))!;
            }

            return await InsertProfileAsync(request, normalized);
        }

        public async Task<GuestAppointmentRecord> InsertAppointmentAsync(GuestBookAppointmentRequest request)
        {
            var normalized = NormalizeMobile(request.MobileNumber);
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            int appointmentId;
            await using (var seqCmd = new OracleCommand(
                "SELECT GUEST_APPOINTMENT_SEQ.NEXTVAL FROM DUAL", conn))
            {
                appointmentId = Convert.ToInt32((await seqCmd.ExecuteScalarAsync())!.ToString());
            }

            await using var cmd = new OracleCommand(@"
                INSERT INTO GUEST_APPOINTMENT (
                    GUEST_APPOINTMENT_ID,
                    GUEST_ID,
                    MOBILE_NUMBER,
                    FULL_NAME,
                    DOCTOR_ID,
                    DOCTOR_NAME,
                    DEPARTMENT_ID,
                    WEEK_ID,
                    APPOINTMENT_TIME,
                    STATUS,
                    PURPOSE,
                    HMIS_APPOINTMENT_ID,
                    CREATED_AT,
                    UPDATED_AT,
                    IS_ACTIVE
                ) VALUES (
                    :id,
                    :guest_id,
                    :mobile_number,
                    :full_name,
                    :doctor_id,
                    :doctor_name,
                    :department_id,
                    :week_id,
                    :appointment_time,
                    :status,
                    :purpose,
                    :hmis_appointment_id,
                    SYSDATE,
                    SYSDATE,
                    'Y'
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = appointmentId;
            cmd.Parameters.Add("guest_id", OracleDbType.Int32).Value =
                request.GuestId.HasValue ? request.GuestId.Value : DBNull.Value;
            cmd.Parameters.Add("mobile_number", OracleDbType.Varchar2).Value = normalized;
            cmd.Parameters.Add("full_name", OracleDbType.Varchar2).Value = request.FullName.Trim();
            cmd.Parameters.Add("doctor_id", OracleDbType.Int32).Value = request.DoctorId;
            cmd.Parameters.Add("doctor_name", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(request.DoctorName)
                    ? (object)DBNull.Value
                    : request.DoctorName.Trim();
            cmd.Parameters.Add("department_id", OracleDbType.Int32).Value =
                request.DepartmentId.HasValue ? request.DepartmentId.Value : DBNull.Value;
            cmd.Parameters.Add("week_id", OracleDbType.Int32).Value =
                request.WeekId.HasValue ? request.WeekId.Value : DBNull.Value;
            cmd.Parameters.Add("appointment_time", OracleDbType.Varchar2).Value =
                request.AppointmentTime.Trim();
            cmd.Parameters.Add("status", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim();
            cmd.Parameters.Add("purpose", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(request.Purpose)
                    ? (object)DBNull.Value
                    : Truncate(request.Purpose.Trim(), 200);
            cmd.Parameters.Add("hmis_appointment_id", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(request.HmisAppointmentId)
                    ? (object)DBNull.Value
                    : request.HmisAppointmentId.Trim();

            await cmd.ExecuteNonQueryAsync();
            return (await GetAppointmentByIdAsync(appointmentId))!;
        }

        public async Task<List<GuestAppointmentRecord>> GetAppointmentsByMobileAsync(string mobileNumber)
        {
            var normalized = NormalizeMobile(mobileNumber);
            var result = new List<GuestAppointmentRecord>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT GUEST_APPOINTMENT_ID, GUEST_ID, MOBILE_NUMBER, FULL_NAME,
                       DOCTOR_ID, DOCTOR_NAME, DEPARTMENT_ID, WEEK_ID,
                       APPOINTMENT_TIME, STATUS, PURPOSE, HMIS_APPOINTMENT_ID,
                       CREATED_AT, UPDATED_AT
                FROM GUEST_APPOINTMENT
                WHERE MOBILE_NUMBER = :mobile_number
                  AND IS_ACTIVE = 'Y'
                ORDER BY CREATED_AT DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mobile_number", OracleDbType.Varchar2).Value = normalized;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(MapAppointment(reader));
            }

            return result;
        }

        public async Task<bool> CancelAppointmentAsync(
            int guestAppointmentId,
            string mobileNumber,
            string reason)
        {
            var normalized = NormalizeMobile(mobileNumber);
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE GUEST_APPOINTMENT
                SET STATUS = 'Cancelled',
                    IS_ACTIVE = 'N',
                    CANCEL_REASON = :reason,
                    UPDATED_AT = SYSDATE
                WHERE GUEST_APPOINTMENT_ID = :id
                  AND MOBILE_NUMBER = :mobile_number
                  AND IS_ACTIVE = 'Y'
                  AND UPPER(STATUS) NOT IN ('CANCELLED', 'COMPLETED')", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("reason", OracleDbType.Varchar2).Value = Truncate(reason.Trim(), 500);
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = guestAppointmentId;
            cmd.Parameters.Add("mobile_number", OracleDbType.Varchar2).Value = normalized;

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private async Task<GuestAppointmentRecord?> GetAppointmentByIdAsync(int id)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT GUEST_APPOINTMENT_ID, GUEST_ID, MOBILE_NUMBER, FULL_NAME,
                       DOCTOR_ID, DOCTOR_NAME, DEPARTMENT_ID, WEEK_ID,
                       APPOINTMENT_TIME, STATUS, PURPOSE, HMIS_APPOINTMENT_ID,
                       CREATED_AT, UPDATED_AT
                FROM GUEST_APPOINTMENT
                WHERE GUEST_APPOINTMENT_ID = :id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return MapAppointment(reader);
        }

        private async Task<GuestProfileRecord> InsertProfileAsync(
            GuestProfileRequest request,
            string normalizedMobile)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO GUEST_PATIENT (
                    GUEST_ID,
                    FULL_NAME,
                    MOBILE_NUMBER,
                    DATE_OF_BIRTH,
                    GENDER,
                    CREATED_AT,
                    UPDATED_AT,
                    IS_ACTIVE
                ) VALUES (
                    GUEST_PATIENT_SEQ.NEXTVAL,
                    :full_name,
                    :mobile_number,
                    :date_of_birth,
                    :gender,
                    SYSDATE,
                    SYSDATE,
                    'Y'
                )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("full_name", OracleDbType.Varchar2).Value = request.FullName.Trim();
            cmd.Parameters.Add("mobile_number", OracleDbType.Varchar2).Value = normalizedMobile;
            cmd.Parameters.Add("date_of_birth", OracleDbType.Date).Value = request.DateOfBirth.Date;
            cmd.Parameters.Add("gender", OracleDbType.Varchar2).Value = request.Gender.Trim();

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return (await GetByMobileAsync(normalizedMobile))!;
        }

        private async Task UpdateProfileAsync(
            int guestId,
            GuestProfileRequest request,
            string normalizedMobile)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE GUEST_PATIENT
                SET FULL_NAME = :full_name,
                    MOBILE_NUMBER = :mobile_number,
                    DATE_OF_BIRTH = :date_of_birth,
                    GENDER = :gender,
                    UPDATED_AT = SYSDATE
                WHERE GUEST_ID = :guest_id
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("full_name", OracleDbType.Varchar2).Value = request.FullName.Trim();
            cmd.Parameters.Add("mobile_number", OracleDbType.Varchar2).Value = normalizedMobile;
            cmd.Parameters.Add("date_of_birth", OracleDbType.Date).Value = request.DateOfBirth.Date;
            cmd.Parameters.Add("gender", OracleDbType.Varchar2).Value = request.Gender.Trim();
            cmd.Parameters.Add("guest_id", OracleDbType.Int32).Value = guestId;

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private static GuestProfileRecord MapGuest(OracleDataReader reader)
        {
            return new GuestProfileRecord
            {
                GuestId = Convert.ToInt32(reader["GUEST_ID"]),
                FullName = reader["FULL_NAME"]?.ToString() ?? string.Empty,
                MobileNumber = reader["MOBILE_NUMBER"]?.ToString() ?? string.Empty,
                DateOfBirth = reader["DATE_OF_BIRTH"] != DBNull.Value
                    ? Convert.ToDateTime(reader["DATE_OF_BIRTH"])
                    : DateTime.MinValue,
                Gender = reader["GENDER"]?.ToString() ?? string.Empty,
                CreatedAt = reader["CREATED_AT"] != DBNull.Value
                    ? Convert.ToDateTime(reader["CREATED_AT"])
                    : DateTime.UtcNow,
                UpdatedAt = reader["UPDATED_AT"] != DBNull.Value
                    ? Convert.ToDateTime(reader["UPDATED_AT"])
                    : DateTime.UtcNow,
            };
        }

        private static GuestAppointmentRecord MapAppointment(OracleDataReader reader)
        {
            return new GuestAppointmentRecord
            {
                GuestAppointmentId = Convert.ToInt32(reader["GUEST_APPOINTMENT_ID"]),
                GuestId = reader["GUEST_ID"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["GUEST_ID"]),
                MobileNumber = reader["MOBILE_NUMBER"]?.ToString() ?? string.Empty,
                FullName = reader["FULL_NAME"]?.ToString() ?? string.Empty,
                DoctorId = Convert.ToInt32(reader["DOCTOR_ID"]),
                DoctorName = reader["DOCTOR_NAME"] == DBNull.Value
                    ? null
                    : reader["DOCTOR_NAME"]?.ToString(),
                DepartmentId = reader["DEPARTMENT_ID"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["DEPARTMENT_ID"]),
                WeekId = reader["WEEK_ID"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["WEEK_ID"]),
                AppointmentTime = reader["APPOINTMENT_TIME"]?.ToString() ?? string.Empty,
                Status = reader["STATUS"]?.ToString() ?? "Pending",
                Purpose = reader["PURPOSE"] == DBNull.Value
                    ? null
                    : reader["PURPOSE"]?.ToString(),
                HmisAppointmentId = reader["HMIS_APPOINTMENT_ID"] == DBNull.Value
                    ? null
                    : reader["HMIS_APPOINTMENT_ID"]?.ToString(),
                CreatedAt = reader["CREATED_AT"] != DBNull.Value
                    ? Convert.ToDateTime(reader["CREATED_AT"])
                    : DateTime.UtcNow,
                UpdatedAt = reader["UPDATED_AT"] != DBNull.Value
                    ? Convert.ToDateTime(reader["UPDATED_AT"])
                    : DateTime.UtcNow,
            };
        }

        private static string Truncate(string value, int max)
        {
            return value.Length <= max ? value : value[..max];
        }

        public static string NormalizeMobile(string raw)
        {
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length >= 12)
            {
                digits = digits[2..];
            }

            if (digits.StartsWith('0') && digits.Length > 1)
            {
                return digits;
            }

            if (digits.Length == 10)
            {
                return $"0{digits}";
            }

            return digits;
        }
    }
}
