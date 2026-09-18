namespace HospitalMobileAPPApi.Models
{
    public class PatientRecentActivityRecord
    {
        public int ActivityId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public string ActivityKey { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string? PayloadJson { get; set; }
        public DateTime ViewedAt { get; set; }
    }

    public class RecordRecentActivityRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>Stable client key (e.g. doctor_12, visit_1001).</summary>
        /// <example>doctor_12</example>
        public string ActivityKey { get; set; } = string.Empty;

        /// <summary>doctor | appointment | medicalReport | discharge | visit | bill</summary>
        /// <example>doctor</example>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Short title shown on dashboard.</summary>
        /// <example>Viewed doctor</example>
        public string Title { get; set; } = string.Empty;

        /// <summary>Supporting line (doctor name, invoice, etc.).</summary>
        /// <example>Dr. Umair</example>
        public string? Subtitle { get; set; }

        /// <summary>Deep-link / reopen payload as JSON object.</summary>
        public object? Payload { get; set; }

        /// <summary>Optional client timestamp (UTC/local ISO). Defaults to server time.</summary>
        public DateTime? ViewedAt { get; set; }
    }
}
