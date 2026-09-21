namespace HospitalMobileAPPApi.Models
{
    public sealed class AnalyticsPeriodDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public sealed class UserAnalyticsDto
    {
        public AnalyticsPeriodDto Period { get; set; } = new();
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int NewUsers { get; set; }
        public int ReturningUsers { get; set; }
        public int MobileRegistrations { get; set; }
        public int HmisPortalUsers { get; set; }
    }

    public sealed class VisitBucketDto
    {
        public string Label { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public int Visits { get; set; }
        public int UniqueUsers { get; set; }
    }

    public sealed class VisitAnalyticsDto
    {
        public AnalyticsPeriodDto Period { get; set; } = new();
        public int TotalVisits { get; set; }
        public int UniqueUsers { get; set; }
        public List<VisitBucketDto> Daily { get; set; } = new();
        public List<VisitBucketDto> Weekly { get; set; } = new();
        public List<VisitBucketDto> Monthly { get; set; } = new();
    }

    public sealed class EngagementUserDto
    {
        public string MrNo { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public int TotalSeconds { get; set; }
    }

    public sealed class EngagementAnalyticsDto
    {
        public AnalyticsPeriodDto Period { get; set; } = new();
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int AvgSessionDurationSeconds { get; set; }
        public int MedianSessionDurationSeconds { get; set; }
        public long TotalTimeSpentSeconds { get; set; }
        public List<EngagementUserDto> TopUsersByTime { get; set; } = new();
        public bool SessionTrackingAvailable { get; set; }
    }

    public sealed class StartAppSessionRequest
    {
        public string? SessionGuid { get; set; }
        public string? Platform { get; set; }
        public string? AppVersion { get; set; }
    }

    public sealed class EndAppSessionRequest
    {
        public string SessionGuid { get; set; } = string.Empty;
        public int? DurationSeconds { get; set; }
    }

    public sealed class AppSessionStartResult
    {
        public string SessionGuid { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
    }

    public static class AnalyticsDateRange
    {
        public const int DefaultDays = 30;
        public const int MaxDays = 366;

        public static (DateTime From, DateTime To) Normalize(DateTime? from, DateTime? to)
        {
            var end = (to ?? DateTime.Now).Date.AddDays(1).AddTicks(-1);
            var start = (from ?? end.AddDays(-DefaultDays + 1).Date).Date;

            if (start > end)
            {
                (start, end) = (end.Date, start.Date.AddDays(1).AddTicks(-1));
            }

            if ((end.Date - start.Date).TotalDays > MaxDays)
            {
                start = end.Date.AddDays(-MaxDays);
            }

            return (start, end);
        }
    }
}
