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

        public DoctorRepository(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        private OracleConnection CreateConnection()
        {
            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            return new OracleConnection(connStr);
        }

        public async Task<(List<DoctorInfo> doctors, int totalCount)> GetDoctorsAsync(int pageNumber, int pageSize)
        {
            try
            {
                using var conn = CreateConnection();

                // ✅ 1. Get total count (separate query)
                var countQuery = "SELECT COUNT(*) FROM DOCTOR";
                int totalCount = await conn.ExecuteScalarAsync<int>(countQuery);

                // ✅ 2. Paginated query (NO semicolon)
                var dataQuery = @"
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
                INNER JOIN SPECIALIZATION s 
                    ON d.SPECIALIZATIONID = s.SPECIALIZATIONID
                ORDER BY d.DOCTOR_ID
            ) inner_query
            WHERE ROWNUM <= :MaxRow
        )
        WHERE rnum > :MinRow";

                var parameters = new
                {
                    MinRow = (pageNumber - 1) * pageSize,
                    MaxRow = pageNumber * pageSize
                };

                var doctorData = await conn.QueryAsync<dynamic>(dataQuery, parameters);

                var doctors = new List<DoctorInfo>();
                int serial = (pageNumber - 1) * pageSize + 1;

                foreach (var doctor in doctorData)
                {
                    var imagePathFromDb = doctor.DOCTOR_IMAGE_PATH?.ToString();
                    string imageUrl = null;

                    if (!string.IsNullOrWhiteSpace(imagePathFromDb))
                    {
                        var cleanPath = imagePathFromDb
                                            .Replace("~", "")
                                            .TrimStart('/');

                        imageUrl = $"http://172.16.40.10:8080/{cleanPath}";
                    }

                    doctors.Add(new DoctorInfo
                    {
                        SerialNumber = serial++,
                        Doctor_ID = Convert.ToInt32(doctor.DOCTOR_ID),
                        DoctorName = doctor.DOCTOR_NAME?.ToString(),
                        DoctorDescription = doctor.DOCTOR_DESCRIPTION?.ToString(),
                        SpecializationName = doctor.SPECIALIZATIONNAME?.ToString(),
                        Department_ID = Convert.ToInt32(doctor.DEPARTMENT_ID),
                        DoctorImagePath = imageUrl
                    });
                }

                return (doctors, totalCount);
            }
            catch (Exception)
            {
                return (new List<DoctorInfo>(), 0);
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
                doctors.Add(new DoctorSchedule
                {
                    SerialNumber = serial++,
                    Doctor_ID = Convert.ToInt32(schedule.DOCTOR_ID),
                    DoctorName = schedule.DOCTOR_NAME?.ToString(),
                    DayName = schedule.DAY_NAME?.ToString(),
                    TimeFrom = schedule.SLOT_TIME_FROM != null
                        ? DateTime.Parse(schedule.SLOT_TIME_FROM.ToString())
                        : (DateTime?)null,
                    TimeTo = schedule.SLOT_TIME_TO != null
                        ? DateTime.Parse(schedule.SLOT_TIME_TO.ToString())
                        : (DateTime?)null,
                    Week_ID = Convert.ToInt32(schedule.WEEK_ID),
                    OPD_Charges = Convert.ToInt32(schedule.OPD_CHARGES),
                });
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
            using var cmd = new OracleCommand(@"
        SELECT * FROM SPECIALIZATION WHERE STATUS = 'Y'
    ", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                doctors.Add(new SpecializationInfo
                {
                    SerialNumber = serial++,

                    // Map ONLY columns that exist in SPECIALIZATION
                    SpecializationId = Convert.ToInt32(reader["SPECIALIZATIONID"]),
                    SpecializationName = reader["SPECIALIZATIONNAME"]?.ToString()
                });
            }

            return doctors;
        }

    }
}