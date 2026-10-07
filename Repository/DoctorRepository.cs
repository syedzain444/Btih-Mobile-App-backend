using Dapper;
using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Data;

namespace HospitalMobileAPPApi.Repository
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DoctorRepository> _logger;

        public DoctorRepository(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<DoctorRepository> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        private OracleConnection CreateConnection()
        {
            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                throw new InvalidOperationException("HOS_WEB_MVC_LIVE connection string is not configured");
            }

            return new OracleConnection(connStr);
        }

        public async Task<(List<DoctorInfo> doctors, int totalCount)> GetDoctorsAsync(int pageNumber, int pageSize)
        {
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                // Count all doctors (specialization is optional via LEFT JOIN below).
                await using (var countCmd = new OracleCommand("SELECT COUNT(*) FROM DOCTOR", conn))
                {
                    var countObj = await countCmd.ExecuteScalarAsync();
                    var totalCount = Convert.ToInt32(countObj);

                    if (totalCount == 0)
                    {
                        return (new List<DoctorInfo>(), 0);
                    }

                    var minRow = (pageNumber - 1) * pageSize;
                    var maxRow = pageNumber * pageSize;

                    await using var dataCmd = new OracleCommand(@"
SELECT * FROM (
    SELECT inner_query.*, ROWNUM rnum
      FROM (
            SELECT d.DOCTOR_NAME,
                   d.DOCTOR_ID,
                   d.DOCTOR_DESCRIPTION,
                   d.DOCTOR_IMAGE_PATH,
                   d.DEPARTMENT_ID,
                   s.SPECIALIZATIONNAME
              FROM DOCTOR d
              LEFT JOIN SPECIALIZATION s
                ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
             ORDER BY d.DOCTOR_ID
      ) inner_query
     WHERE ROWNUM <= :MaxRow
)
 WHERE rnum > :MinRow", conn);

                    dataCmd.BindByName = true;
                    dataCmd.Parameters.Add("MaxRow", OracleDbType.Int32).Value = maxRow;
                    dataCmd.Parameters.Add("MinRow", OracleDbType.Int32).Value = minRow;

                    var doctors = new List<DoctorInfo>();
                    var serial = minRow + 1;

                    await using var reader = await dataCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        doctors.Add(new DoctorInfo
                        {
                            SerialNumber = serial++,
                            Doctor_ID = reader["DOCTOR_ID"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["DOCTOR_ID"]),
                            DoctorName = reader["DOCTOR_NAME"]?.ToString(),
                            DoctorDescription = reader["DOCTOR_DESCRIPTION"]?.ToString(),
                            SpecializationName = reader["SPECIALIZATIONNAME"] == DBNull.Value
                                ? null
                                : reader["SPECIALIZATIONNAME"]?.ToString(),
                            Department_ID = reader["DEPARTMENT_ID"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["DEPARTMENT_ID"]),
                            DoctorImagePath = BuildDoctorImageUrl(
                                reader["DOCTOR_IMAGE_PATH"] == DBNull.Value
                                    ? null
                                    : reader["DOCTOR_IMAGE_PATH"]?.ToString()),
                        });
                    }

                    return (doctors, totalCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDoctorsAsync failed for page {PageNumber} size {PageSize}", pageNumber, pageSize);
                throw;
            }
        }

        private string? BuildDoctorImageUrl(string? imagePathFromDb)
        {
            if (string.IsNullOrWhiteSpace(imagePathFromDb))
            {
                return null;
            }

            try
            {
                var cleanPath = imagePathFromDb
                    .Replace("~", string.Empty, StringComparison.Ordinal)
                    .Trim()
                    .TrimStart('/', '\\')
                    .Replace('\\', '/');

                if (cleanPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    cleanPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (Uri.TryCreate(cleanPath, UriKind.Absolute, out var absolute) &&
                        !string.IsNullOrWhiteSpace(absolute.AbsolutePath))
                    {
                        cleanPath = absolute.AbsolutePath.TrimStart('/');
                    }
                }

                if (string.IsNullOrWhiteSpace(cleanPath))
                {
                    return null;
                }

                var encoded = EncodePathKeepSlashes(cleanPath);
                var configuredBase = _configuration["DoctorImages:BaseUrl"];
                if (!string.IsNullOrWhiteSpace(configuredBase) &&
                    !IsLegacyImageHost(configuredBase))
                {
                    return $"{configuredBase.TrimEnd('/')}/{encoded}";
                }

                return "/" + encoded;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not build doctor image URL from {Path}", imagePathFromDb);
                return null;
            }
        }

        //public async Task<List<DoctorInfo>> GetDoctorsAsync()
        //{
        //    var doctors = new List<DoctorInfo>();
        //    int serial = 1;

        //    var query = @"
        //        SELECT d.DOCTOR_NAME, d.DOCTOR_ID, d.DOCTOR_DESCRIPTION, d.DOCTOR_IMAGE_PATH, d.DEPARTMENT_ID,
        //        s.SPECIALIZATIONNAME FROM DOCTOR d 
        //        INNER JOIN SPECIALIZATION s 
        //        ON d.SPECIALIZATIONID = s.SPECIALIZATIONID";

        //    using var conn = CreateConnection();
        //    var doctorData = await conn.QueryAsync<dynamic>(query);

        //    foreach (var doctor in doctorData)
        //    {
        //        var imageName = doctor.DOCTOR_IMAGE_PATH?.ToString();
        //        string imageUrl = null;

        //        if (!string.IsNullOrEmpty(imageName))
        //        {
        //            var relativePath = imageName.TrimStart('~').TrimStart('/');
        //            var physicalPath = Path.Combine(_environment.WebRootPath, relativePath);

        //            // Check if image has transparency and preserve it
        //            var processedFileName = await ProcessImagePreserveTransparencyAsync(physicalPath);

        //            if (!string.IsNullOrEmpty(processedFileName))
        //            {
        //                imageUrl = $"http://172.16.40.10:8080/{relativePath.Replace(Path.GetFileName(relativePath), processedFileName)}";
        //            }
        //            else
        //            {
        //                // Fallback to original if processing fails
        //                imageUrl = $"http://172.16.40.10:8080/{relativePath}";
        //            }
        //        }

        //        doctors.Add(new DoctorInfo
        //        {
        //            SerialNumber = serial++,
        //            Doctor_ID = Convert.ToInt32(doctor.DOCTOR_ID),
        //            DoctorName = doctor.DOCTOR_NAME?.ToString(),
        //            DoctorDescription = doctor.DOCTOR_DESCRIPTION?.ToString(),
        //            SpecializationName = doctor.SPECIALIZATIONNAME?.ToString(),
        //            Department_ID = Convert.ToInt32(doctor.DEPARTMENT_ID),
        //            DoctorImagePath = imageUrl
        //        });
        //    }

        //    return doctors;
        //}

        public async Task<List<DoctorSchedule>> GetDoctorScheduleAsync(int doctorId)
        {
            const int slotMinutes = 15;

            var query = @"
                SELECT d.DOCTOR_NAME, d.DOCTOR_ID, os.OPD_ID, wd.DAY_NAME, 
                       os.TIME_FROM AS SLOT_TIME_FROM, os.TIME_TO AS SLOT_TIME_TO,
                       os.WEEK_ID , os.OPD_CHARGES 
                FROM OPD_SCHEDULE os 
                INNER JOIN WEEK_DAYS wd ON wd.WEEK_ID = os.WEEK_ID 
                INNER JOIN DOCTOR d ON d.DOCTOR_ID = os.DOCTOR_ID 
                WHERE os.DOCTOR_ID = :DoctorId 
                ORDER BY os.WEEK_ID, os.TIME_FROM";

            using var conn = CreateConnection();
            var scheduleData = await conn.QueryAsync<dynamic>(query, new { DoctorId = doctorId });

            var doctors = new List<DoctorSchedule>();
            int serial = 1;

            foreach (var schedule in scheduleData)
            {
                DateTime? timeFrom = schedule.SLOT_TIME_FROM != null
                    ? DateTime.Parse(schedule.SLOT_TIME_FROM.ToString())
                    : null;
                DateTime? timeTo = schedule.SLOT_TIME_TO != null
                    ? DateTime.Parse(schedule.SLOT_TIME_TO.ToString())
                    : null;

                var doctorIdValue = Convert.ToInt32(schedule.DOCTOR_ID);
                var doctorName = schedule.DOCTOR_NAME?.ToString();
                var dayName = schedule.DAY_NAME?.ToString();
                var weekId = Convert.ToInt32(schedule.WEEK_ID);
                var charges = Convert.ToInt32(schedule.OPD_CHARGES);

                if (timeFrom == null || timeTo == null || timeTo <= timeFrom)
                {
                    doctors.Add(new DoctorSchedule
                    {
                        SerialNumber = serial++,
                        Doctor_ID = doctorIdValue,
                        DoctorName = doctorName,
                        DayName = dayName,
                        TimeFrom = timeFrom,
                        TimeTo = timeTo,
                        Week_ID = weekId,
                        OPD_Charges = charges,
                    });
                    continue;
                }

                // Expand OPD windows (often stored as hourly blocks) into 15-minute booking slots.
                var cursor = timeFrom.Value;
                var end = timeTo.Value;
                while (cursor < end)
                {
                    var slotEnd = cursor.AddMinutes(slotMinutes);
                    if (slotEnd > end)
                    {
                        slotEnd = end;
                    }

                    // Skip leftover fragments shorter than a full slot when the window is not aligned.
                    var spanMinutes = (slotEnd - cursor).TotalMinutes;
                    if (spanMinutes < slotMinutes && cursor > timeFrom.Value)
                    {
                        break;
                    }

                    doctors.Add(new DoctorSchedule
                    {
                        SerialNumber = serial++,
                        Doctor_ID = doctorIdValue,
                        DoctorName = doctorName,
                        DayName = dayName,
                        TimeFrom = cursor,
                        TimeTo = cursor.AddMinutes(slotMinutes) <= end
                            ? cursor.AddMinutes(slotMinutes)
                            : end,
                        Week_ID = weekId,
                        OPD_Charges = charges,
                    });

                    cursor = cursor.AddMinutes(slotMinutes);
                }
            }

            return doctors;
        }
        
        private async Task<string> ProcessImagePreserveTransparencyAsync(string originalPath)
        {
            if (!File.Exists(originalPath))
                return null;

            var directory = Path.GetDirectoryName(originalPath);
            var extension = Path.GetExtension(originalPath).ToLowerInvariant();
            var fileName = "mobile_" + Path.GetFileName(originalPath);
            var processedPath = Path.Combine(directory, fileName);

            // If already processed, don't recreate
            if (File.Exists(processedPath))
                return fileName;

            try
            {
                using var image = await Image.LoadAsync(originalPath);

                // Check if image has transparency (PNG)
                bool hasTransparency = extension == ".png";

                // Resize the image
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(350, 0),
                    Mode = ResizeMode.Max
                }));

                // Save based on original format to preserve transparency
                if (hasTransparency)
                {
                    // Save as PNG to preserve transparency
                    await image.SaveAsync(processedPath.Replace(".jpg", ".png").Replace(".jpeg", ".png"),
                        new PngEncoder
                        {
                            CompressionLevel = PngCompressionLevel.BestCompression,
                            TransparentColorMode = PngTransparentColorMode.Preserve
                        });

                    return fileName.Replace(".jpg", ".png").Replace(".jpeg", ".png");
                }
                else
                {
                    // For non-transparent images, save as JPEG with white background
                    using var jpegImage = image.Clone(ctx =>
                        ctx.BackgroundColor(Color.White));

                    await jpegImage.SaveAsync(processedPath, new JpegEncoder
                    {
                        Quality = 75
                    });

                    return fileName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing image {originalPath}: {ex.Message}");
                return null;
            }
        }
        public async Task<List<SpecializationInfo>> GetDoctorSpecialization()
        {
            var doctors = new List<SpecializationInfo>();
            int serial = 1;

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");

            using var conn = new OracleConnection(connStr);
            // Only specializations that currently have doctors — keeps the app filter
            // in sync when DOCTOR / SPECIALIZATION data is refreshed.
            using var cmd = new OracleCommand(@"
                SELECT DISTINCT
                       s.SPECIALIZATIONID,
                       s.SPECIALIZATIONNAME
                FROM SPECIALIZATION s
                INNER JOIN DOCTOR d
                    ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                WHERE s.SPECIALIZATIONNAME IS NOT NULL
                ORDER BY s.SPECIALIZATIONNAME
            ", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var name = reader["SPECIALIZATIONNAME"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                doctors.Add(new SpecializationInfo
                {
                    SerialNumber = serial++,
                    SpecializationId = Convert.ToInt32(reader["SPECIALIZATIONID"]),
                    SpecializationName = name,
                });
            }

            return doctors;
        }

        private static bool IsLegacyImageHost(string hostOrUrl)
        {
            if (string.IsNullOrWhiteSpace(hostOrUrl))
            {
                return true;
            }

            var host = hostOrUrl.Trim();
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
            }

            return host.Equals("172.16.40.10", StringComparison.OrdinalIgnoreCase)
                || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        private static string EncodePathKeepSlashes(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return string.Join(
                "/",
                path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(segment => Uri.EscapeDataString(segment)));
        }
    }
}