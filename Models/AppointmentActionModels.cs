namespace HospitalMobileAPPApi.Models
{
    /// <summary>Patient-initiated appointment cancellation.</summary>
    public class CancelAppointmentRequest
    {
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <example>Unable to travel due to illness</example>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Patient-initiated reschedule request — pending admin approval.</summary>
    public class RescheduleAppointmentRequest
    {
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <example>Work meeting conflict</example>
        public string Reason { get; set; } = string.Empty;

        public int WeekId { get; set; }

        /// <example>Friday: 10:00 AM</example>
        public string AppointmentTime { get; set; } = string.Empty;

        public int? DoctorId { get; set; }

        public int? DepartmentId { get; set; }
    }
}
