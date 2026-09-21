namespace HospitalMobileAPPApi.Models
{
    public sealed class AdminPortalUserDto
    {
        public string MrNo { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ContactNo { get; set; }
        public string? Email { get; set; }
        public string Source { get; set; } = string.Empty;
        public bool ProfileSetupComplete { get; set; }
        public DateTime? RegisteredAt { get; set; }
    }

    public sealed class AdminAppointmentActionRequest
    {
        public string? Notes { get; set; }
    }

    public sealed class ReportBucketDto
    {
        public string Label { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public int Count { get; set; }
    }

    public sealed class StatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class RegistrationReportDto
    {
        public AnalyticsPeriodDto Period { get; set; } = new();
        public int TotalRegistrations { get; set; }
        public int MobileRegistrations { get; set; }
        public int HmisPortalUsers { get; set; }
        public List<ReportBucketDto> Daily { get; set; } = new();
    }

    public sealed class AppointmentReportDto
    {
        public AnalyticsPeriodDto Period { get; set; } = new();
        public string? StatusFilter { get; set; }
        public int TotalAppointments { get; set; }
        public List<StatusCountDto> ByStatus { get; set; } = new();
        public List<ReportBucketDto> Daily { get; set; } = new();
    }

    public static class AdminPagination
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        public static (int PageNumber, int PageSize, int Skip) Normalize(int pageNumber, int pageSize)
        {
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
            return (pageNumber, pageSize, (pageNumber - 1) * pageSize);
        }

        public static int TotalPages(int totalRecords, int pageSize) =>
            pageSize <= 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);
    }
}
