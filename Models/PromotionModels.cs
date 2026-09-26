namespace HospitalMobileAPPApi.Models
{
    public class MobilePromotionRecord
    {
        public int PromotionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int DurationSeconds { get; set; } = 5;
        public bool IsActive { get; set; } = true;
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePromotionRequest
    {
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int DurationSeconds { get; set; } = 5;
        public bool IsActive { get; set; } = true;
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
    }

    public class UpdatePromotionRequest
    {
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int DurationSeconds { get; set; } = 5;
        public bool IsActive { get; set; } = true;
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
    }

    /// <summary>Multipart form fields from admin panel promotion upload.</summary>
    public class PromotionMultipartForm
    {
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int DurationSeconds { get; set; } = 5;
        public string? IsActive { get; set; } = "true";
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool ParseIsActive() =>
            !string.Equals(IsActive, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(IsActive, "0", StringComparison.OrdinalIgnoreCase);
    }
}
