namespace HospitalMobileAPPApi.Models
{
    /// <summary>Appointment booking request. Supports guest booking (mrno optional).</summary>
    public class AppointmentModel
    {
        /// <summary>Patient full name (required).</summary>
        /// <example>Ali Khan</example>
        public string? name { get; set; }

        /// <summary>Contact phone number (required).</summary>
        /// <example>03001234567</example>
        public string? phoneNo { get; set; }

        /// <summary>Patient MR number (optional for guest booking).</summary>
        /// <example>010-002-152</example>
        public string? mrno { get; set; }

        /// <summary>Email address (optional).</summary>
        public string? email { get; set; }

        /// <summary>Schedule week/slot identifier from doctor schedule.</summary>
        /// <example>12</example>
        public int weekId { get; set; }

        /// <summary>Requested appointment time label.</summary>
        /// <example>10:00 AM</example>
        public string appointment_time { get; set; } = string.Empty;

        /// <summary>Appointment status (e.g. Pending).</summary>
        /// <example>Pending</example>
        public string status { get; set; } = string.Empty;

        /// <summary>Doctor ID.</summary>
        /// <example>101</example>
        public int doctorId { get; set; }

        /// <summary>Department ID.</summary>
        /// <example>5</example>
        public int departmentId { get; set; }

        /// <summary>Visit purpose / reason.</summary>
        /// <example>Follow-up consultation</example>
        public string purpose { get; set; } = string.Empty;

        /// <summary>Record creation timestamp.</summary>
        public DateTime? createdAt { get; set; }

        /// <summary>Active flag (Y/N).</summary>
        /// <example>Y</example>
        public string isActive { get; set; } = "Y";

        /// <summary>Entry date.</summary>
        public DateTime? entryDate { get; set; }
    }
}
