using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class RegistrationRepository : IRegistrationRepository
    {
        private readonly IConfiguration _configuration;

        public RegistrationRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> HasPortalPasswordAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_MST
                WHERE MR_NO = :mr_no
                  AND PATIENT_PASSWORD IS NOT NULL
                  AND TRIM(PATIENT_PASSWORD) IS NOT NULL", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<bool> IsPhoneRegisteredAsync(string contactNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_INFORMATION
                WHERE CONTACT_NO = :contact_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("contact_no", contactNo));

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<bool> IsMobilePhoneRegisteredAsync(string contactNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM MOBILE_PATIENT_REGISTRATION
                WHERE CONTACT_NO = :contact_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("contact_no", contactNo));

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<MobilePatientRecord?> GetMobilePatientByPhoneAsync(string contactNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT MR_NO, CONTACT_NO, FIRST_NAME, LAST_NAME, PATIENT_PASSWORD,
                       CASE WHEN PROFILE_SETUP_COMPLETE = 'Y' THEN 1 ELSE 0 END AS PROFILE_DONE
                FROM MOBILE_PATIENT_REGISTRATION
                WHERE CONTACT_NO = :contact_no
                  AND IS_ACTIVE = 'Y'
                  AND ROWNUM = 1", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("contact_no", contactNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return MapMobilePatient(reader);
        }

        public async Task<MobilePatientRecord?> GetMobilePatientByMrNoAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT MR_NO, CONTACT_NO, FIRST_NAME, LAST_NAME, PATIENT_PASSWORD,
                       CASE WHEN PROFILE_SETUP_COMPLETE = 'Y' THEN 1 ELSE 0 END AS PROFILE_DONE
                FROM MOBILE_PATIENT_REGISTRATION
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'
                  AND ROWNUM = 1", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return MapMobilePatient(reader);
        }

        public async Task<string> CreateMobilePatientAsync(
            string contactNo,
            string firstName,
            string? lastName,
            string password,
            bool acceptedTerms)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_PATIENT_REGISTRATION (
                    CONTACT_NO, FIRST_NAME, LAST_NAME, PATIENT_PASSWORD, ACCEPTED_TERMS
                ) VALUES (
                    :contact_no, :first_name, :last_name, :password, :accepted_terms
                )
                RETURNING MR_NO INTO :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("contact_no", contactNo));
            cmd.Parameters.Add(new OracleParameter("first_name", firstName));
            cmd.Parameters.Add(new OracleParameter("last_name", lastName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("password", password));
            cmd.Parameters.Add(new OracleParameter("accepted_terms", acceptedTerms ? "Y" : "N"));

            var mrOut = new OracleParameter("mr_no", OracleDbType.Varchar2, 30)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(mrOut);

            await cmd.ExecuteNonQueryAsync();
            return mrOut.Value?.ToString() ?? string.Empty;
        }

        public async Task<bool> UpdateHmisPatientNamesAsync(string mrNo, string? firstName, string? lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            {
                return true;
            }

            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_MST
                SET FIRST_NAME = NVL(:first_name, FIRST_NAME),
                    LAST_NAME = NVL(:last_name, LAST_NAME)
                WHERE MR_NO = :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("first_name", firstName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("last_name", lastName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> CompleteMobileProfileSetupAsync(ProfileSetupRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_PATIENT_REGISTRATION
                SET FIRST_NAME = NVL(:first_name, FIRST_NAME),
                    LAST_NAME = NVL(:last_name, LAST_NAME),
                    CNIC = :cnic,
                    DATE_OF_BIRTH = :date_of_birth,
                    GENDER = :gender,
                    BLOOD_GROUP = :blood_group,
                    EMAIL_ADDRESS = :email,
                    PROFILE_SETUP_COMPLETE = 'Y',
                    UPDATED_AT = SYSDATE
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("first_name", request.FirstName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("last_name", request.LastName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("cnic", request.CNIC ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("date_of_birth", request.DateOfBirth ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("gender", NormalizeGender(request.Gender) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("blood_group", request.BloodGroup ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("email", request.Email ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> IsProfileSetupRequiredAsync(string mrNo)
        {
            if (mrNo.StartsWith("MOB-", StringComparison.OrdinalIgnoreCase))
            {
                var mobile = await GetMobilePatientByMrNoAsync(mrNo);
                return mobile != null && !mobile.ProfileSetupComplete;
            }

            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_MST pm
                INNER JOIN PATIENT_INFORMATION pi ON pi.MR_NO = pm.MR_NO
                WHERE pm.MR_NO = :mr_no
                  AND (
                        pi.CNIC IS NULL OR TRIM(pi.CNIC) IS NULL
                     OR pi.DT_DOB IS NULL
                     OR pm.GENDER IS NULL OR TRIM(pm.GENDER) IS NULL
                  )", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<PatientDetails?> GetMobilePatientDetailsAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT MR_NO, CONTACT_NO, FIRST_NAME, LAST_NAME, CNIC, DATE_OF_BIRTH,
                       GENDER, BLOOD_GROUP, EMAIL_ADDRESS
                FROM MOBILE_PATIENT_REGISTRATION
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'
                  AND ROWNUM = 1", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new PatientDetails
            {
                MrNo = reader["MR_NO"]?.ToString(),
                ContactNo = reader["CONTACT_NO"]?.ToString(),
                FirstName = reader["FIRST_NAME"]?.ToString(),
                LastName = reader["LAST_NAME"]?.ToString(),
                CNIC = reader["CNIC"]?.ToString(),
                DateOfBirth = reader["DATE_OF_BIRTH"] != DBNull.Value
                    ? Convert.ToDateTime(reader["DATE_OF_BIRTH"])
                    : null,
                Gender = reader["GENDER"]?.ToString(),
                BloodGroup = reader["BLOOD_GROUP"]?.ToString(),
                EmailAddress = reader["EMAIL_ADDRESS"]?.ToString(),
            };
        }

        private static MobilePatientRecord MapMobilePatient(OracleDataReader reader)
        {
            return new MobilePatientRecord
            {
                MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                ContactNo = reader["CONTACT_NO"]?.ToString() ?? string.Empty,
                FirstName = reader["FIRST_NAME"]?.ToString(),
                LastName = reader["LAST_NAME"]?.ToString(),
                Password = reader["PATIENT_PASSWORD"]?.ToString() ?? string.Empty,
                ProfileSetupComplete = Convert.ToInt32(reader["PROFILE_DONE"]) == 1,
            };
        }

        private static string? NormalizeGender(string? gender)
        {
            if (string.IsNullOrWhiteSpace(gender))
            {
                return null;
            }

            var value = gender.Trim().ToUpperInvariant();
            return value switch
            {
                "M" or "MALE" => "M",
                "F" or "FEMALE" => "F",
                _ => gender.Trim(),
            };
        }
    }
}
