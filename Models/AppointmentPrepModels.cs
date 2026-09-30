namespace HospitalMobileAPPApi.Models
{
    public static class AppointmentPrepKinds
    {
        public const string Radiology = "RADIOLOGY";
        public const string Gastro = "GASTRO";
    }

    public static class AppointmentPrepAlertStatuses
    {
        public const string Pending = "PENDING";
        public const string Sent = "SENT";
        public const string Cancelled = "CANCELLED";
        public const string Failed = "FAILED";
    }

    public class AppointmentPrepAlert
    {
        public int AlertId { get; set; }
        public string AppointmentId { get; set; } = string.Empty;
        public string MrNo { get; set; } = string.Empty;
        public string PrepKind { get; set; } = string.Empty;
        public string? DepartmentHint { get; set; }
        public string? DoctorName { get; set; }
        public DateTime AppointmentAt { get; set; }
        public DateTime SendAt { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Status { get; set; } = AppointmentPrepAlertStatuses.Pending;
        public DateTime? SentAt { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Upcoming hospital appointment row candidate for prep matching.</summary>
    public class PrepEligibleAppointment
    {
        public string AppointmentId { get; set; } = string.Empty;
        public string? MrNo { get; set; }
        public string? DoctorName { get; set; }
        public int DoctorId { get; set; }
        public int DepartmentId { get; set; }
        public string? SpecializationName { get; set; }
        public string? Purpose { get; set; }
        public string? AppointmentTimeLabel { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EntryDate { get; set; }
    }

    public class SendFastingReminderRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string PrepKind { get; set; } = AppointmentPrepKinds.Radiology;
        public string? AppointmentId { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
    }
}
