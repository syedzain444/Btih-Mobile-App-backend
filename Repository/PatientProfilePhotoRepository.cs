using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IPatientProfilePhotoRepository
    {
        Task<string?> GetImagePathAsync(string mrNo);
        Task UpsertAsync(string mrNo, string imagePath, string? contentType, long fileSize);
        Task<bool> ClearAsync(string mrNo);
    }

    public class PatientProfilePhotoRepository : IPatientProfilePhotoRepository
    {
        private readonly IConfiguration _configuration;

        public PatientProfilePhotoRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string?> GetImagePathAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT IMAGE_PATH
                FROM PATIENT_PROFILE_PHOTO
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            var path = result?.ToString();
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }

        public async Task UpsertAsync(
            string mrNo,
            string imagePath,
            string? contentType,
            long fileSize)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var merge = new OracleCommand(@"
                MERGE INTO PATIENT_PROFILE_PHOTO t
                USING (SELECT :mr_no AS MR_NO FROM dual) s
                ON (t.MR_NO = s.MR_NO)
                WHEN MATCHED THEN
                    UPDATE SET
                        IMAGE_PATH = :image_path,
                        CONTENT_TYPE = :content_type,
                        FILE_SIZE = :file_size,
                        UPDATED_AT = SYSDATE,
                        IS_ACTIVE = 'Y'
                WHEN NOT MATCHED THEN
                    INSERT (
                        PHOTO_ID,
                        MR_NO,
                        IMAGE_PATH,
                        CONTENT_TYPE,
                        FILE_SIZE,
                        CREATED_AT,
                        UPDATED_AT,
                        IS_ACTIVE
                    ) VALUES (
                        PATIENT_PROFILE_PHOTO_SEQ.NEXTVAL,
                        :mr_no,
                        :image_path,
                        :content_type,
                        :file_size,
                        SYSDATE,
                        SYSDATE,
                        'Y'
                    )", conn);

            merge.BindByName = true;
            merge.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();
            merge.Parameters.Add("image_path", OracleDbType.Varchar2).Value = imagePath;
            merge.Parameters.Add("content_type", OracleDbType.Varchar2).Value =
                (object?)contentType ?? DBNull.Value;
            merge.Parameters.Add("file_size", OracleDbType.Int64).Value = fileSize;
            await merge.ExecuteNonQueryAsync();
        }

        public async Task<bool> ClearAsync(string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_PROFILE_PHOTO
                SET IS_ACTIVE = 'N',
                    UPDATED_AT = SYSDATE
                WHERE MR_NO = :mr_no
                  AND IS_ACTIVE = 'Y'", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }
}
