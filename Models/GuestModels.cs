namespace HospitalMobileAPPApi.Models
{
    public class GuestProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
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
}
