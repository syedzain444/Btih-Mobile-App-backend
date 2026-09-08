namespace HospitalMobileAPPApi.Repository
{
    using HospitalMobileAPPApi.Models;
    using Oracle.ManagedDataAccess.Client;

    public class AuthRepository : IAuthRepository
    {
        private readonly IConfiguration _configuration;

        public AuthRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<LoginResponse?> LoginAsync(string contactNo, string password)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
        SELECT pi.MR_NO, pm.FIRST_NAME
        FROM PATIENT_MST pm
        INNER JOIN PATIENT_INFORMATION pi
            ON pi.MR_NO = pm.MR_NO
        WHERE pi.CONTACT_NO = :CONTACT_NO
          AND pm.PATIENT_PASSWORD = :PATIENT_PASSWORD
          AND ROWNUM = 1
    ", conn);

            cmd.Parameters.Add(new OracleParameter("CONTACT_NO", contactNo));
            cmd.Parameters.Add(new OracleParameter("PATIENT_PASSWORD", password));

            await conn.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new LoginResponse
                {
                    MrNo = reader["MR_NO"]?.ToString(),
                    FirstName = reader["FIRST_NAME"]?.ToString()
                };
            }

            return null;
        }

        public async Task<(string? MR_NO, string? CONTACT_NO)> VerifyPhoneNo(string? contactNo, string? mrno)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            try
            {
                await using var conn = new OracleConnection(connStr);
                await using var cmd = new OracleCommand(@"
            SELECT CONTACT_NO, MR_NO
            FROM PATIENT_INFORMATION
            WHERE (CONTACT_NO = :contactNo OR MR_NO = :mrno)
            AND ROWNUM = 1
        ", conn);

                cmd.Parameters.Add(new OracleParameter("contactNo", contactNo));
                cmd.Parameters.Add(new OracleParameter("mrno", mrno));

                await conn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return (
                        reader["MR_NO"]?.ToString(),
                        reader["CONTACT_NO"]?.ToString()
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return (null, null);
        }

        public async Task<string?> GetMrNoByContactNoAsync(string contactNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT MR_NO
                FROM PATIENT_INFORMATION
                WHERE CONTACT_NO = :contactNo
                  AND ROWNUM = 1", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("contactNo", contactNo));

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        public async Task<bool> ContactBelongsToMrNoAsync(string contactNo, string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_INFORMATION
                WHERE CONTACT_NO = :contactNo
                  AND MR_NO = :mrNo", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("contactNo", contactNo));
            cmd.Parameters.Add(new OracleParameter("mrNo", mrNo));

            await conn.OpenAsync();
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }
    }
}