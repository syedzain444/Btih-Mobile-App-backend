namespace HospitalMobileAPPApi.Models
{
    public class PatientNotificationRecord
    {
        public int NotificationId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public string Category { get; set; } = "general";
        public string Priority { get; set; } = "normal";
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? PayloadJson { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class NotificationInboxResult
    {
        public List<PatientNotificationRecord> Data { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int UnreadCount { get; set; }
    }

    public class MarkNotificationReadRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;
    }

    public class RecordNotificationRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>Notification type key (appointment_reminder, report_ready, etc.).</summary>
        /// <example>appointment_reminder</example>
        public string NotificationType { get; set; } = string.Empty;

        /// <summary>Optional category: appointments, lab, records, billing, general.</summary>
        /// <example>appointments</example>
        public string? Category { get; set; }

        /// <summary>Optional priority: high, normal, low.</summary>
        /// <example>normal</example>
        public string? Priority { get; set; }

        /// <summary>Notification title.</summary>
        /// <example>Appointment Reminder</example>
        public string Title { get; set; } = string.Empty;

        /// <summary>Notification body.</summary>
        /// <example>Your appointment is tomorrow at 10:00 AM.</example>
        public string Body { get; set; } = string.Empty;

        /// <summary>Optional navigation/deep-link payload.</summary>
        public Dictionary<string, string>? Payload { get; set; }
    }
}
