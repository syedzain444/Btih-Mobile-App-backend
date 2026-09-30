namespace HospitalMobileAPPApi.Configuration
{
    /// <summary>Pre-appointment fasting / preparation alerts for Radiology and Gastro (REQ-2026-009).</summary>
    public class AppointmentPrepSettings
    {
        public const string SectionName = "AppointmentPrep";

        public bool EnableServerPush { get; set; } = true;
        public int PollIntervalSeconds { get; set; } = 60;

        /// <summary>How far ahead to look when discovering appointments that need prep alerts.</summary>
        public int LookaheadDays { get; set; } = 21;

        public PrepRuleSettings Radiology { get; set; } = new()
        {
            HoursBefore = 12,
            Title = "Radiology preparation reminder",
            Instructions =
                "Please remain fasting (no food or drink except sips of water if allowed by your doctor) " +
                "for at least 6–8 hours before your radiology appointment, unless your doctor advised otherwise. " +
                "Bring prior films/CDs and your MR card. Arrive 15 minutes early.",
            MatchKeywords = new[]
            {
                "RADIOLOGY", "RADIOLOG", "IMAGING", "X-RAY", "XRAY", "CT SCAN", "CT ",
                "MRI", "ULTRASOUND", "SONOGRAPHY", "FLUORO", "MAMMO", "NUCLEAR"
            },
        };

        public PrepRuleSettings Gastro { get; set; } = new()
        {
            HoursBefore = 8,
            Title = "Gastroenterology fasting reminder",
            Instructions =
                "Please remain fasting (no solid food) for at least 8 hours before your gastroenterology / endoscopy " +
                "appointment. Clear fluids may be allowed until 2–4 hours before the procedure — follow your " +
                "doctor's written instructions. Do not take antacids unless advised. Bring a responsible adult if sedation is planned.",
            MatchKeywords = new[]
            {
                "GASTRO", "GASTROENTEROLOGY", "ENDOSCOPY", "COLONOSCOPY", "EGD",
                "ERCP", "HEPATOBILIARY", "DIGESTIVE"
            },
        };
    }

    public class PrepRuleSettings
    {
        public int HoursBefore { get; set; } = 8;
        public string Title { get; set; } = "Appointment preparation reminder";
        public string Instructions { get; set; } = string.Empty;
        public string[] MatchKeywords { get; set; } = Array.Empty<string>();
    }
}
