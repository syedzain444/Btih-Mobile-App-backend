namespace HospitalMobileAPPApi.Models
{
    public class MobileOfferRecord
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? Description { get; set; }
        /// <summary>Package | Offer | Checkup | Campaign</summary>
        public string Category { get; set; } = "Package";
        public string? ImageUrl { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal? OfferPrice { get; set; }
        public string Currency { get; set; } = "PKR";
        /// <summary>Pipe-separated bullet points stored in HIGHLIGHTS.</summary>
        public string? Highlights { get; set; }
        public string CtaLabel { get; set; } = "Enquire";
        public string? CtaPhone { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<string> HighlightList =>
            string.IsNullOrWhiteSpace(Highlights)
                ? new List<string>()
                : Highlights
                    .Split(new[] { '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToList();
    }

    /// <summary>Multipart form fields from admin panel offer upload.</summary>
    public class OfferMultipartForm
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; } = "Package";
        public string? OriginalPrice { get; set; }
        public string? OfferPrice { get; set; }
        public string? Currency { get; set; } = "PKR";
        public string? Highlights { get; set; }
        public string? CtaLabel { get; set; } = "Enquire";
        public string? CtaPhone { get; set; }
        public int SortOrder { get; set; }
        public string? IsActive { get; set; } = "true";
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool ParseIsActive() =>
            !string.Equals(IsActive, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(IsActive, "0", StringComparison.OrdinalIgnoreCase);

        public decimal? ParseOriginalPrice() => ParseDecimal(OriginalPrice);
        public decimal? ParseOfferPrice() => ParseDecimal(OfferPrice);

        private static decimal? ParseDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return decimal.TryParse(raw.Trim(), out var value) ? value : null;
        }
    }
}
