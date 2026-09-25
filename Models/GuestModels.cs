namespace HospitalMobileAPPApi.Models
{
    public class GuestProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        /// <summary>OTP from send-registration-otp — required to prove phone ownership.</summary>
        public string? Otp { get; set; }
    }

    public class GuestProfileRecord
    {
        public int GuestId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class GuestCancelAppointmentRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class GuestBookAppointmentRequest
    {
        public string MobileNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int? GuestId { get; set; }
        public int DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public int? DepartmentId { get; set; }
        public int? WeekId { get; set; }
        public string AppointmentTime { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Purpose { get; set; }
        public string? HmisAppointmentId { get; set; }
    }

    public class GuestAppointmentRecord
    {
        public int GuestAppointmentId { get; set; }
        public int? GuestId { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public int? DepartmentId { get; set; }
        public int? WeekId { get; set; }
        public string AppointmentTime { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string? Purpose { get; set; }
        public string? HmisAppointmentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
