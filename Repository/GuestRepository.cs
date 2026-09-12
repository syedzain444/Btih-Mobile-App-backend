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
