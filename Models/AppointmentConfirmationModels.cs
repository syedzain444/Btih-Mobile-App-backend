namespace HospitalMobileAPPApi.Models
{
    public class AppointmentConfirmationQrDto
    {
        public string AppointmentId { get; set; } = string.Empty;
        public string QrToken { get; set; } = string.Empty;
        public string QrPayload { get; set; } = string.Empty;
        public string? MrNo { get; set; }
        public string? PatientName { get; set; }
        public string? Phone { get; set; }
        public string? DoctorName { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentHint { get; set; }
        public string? AppointmentTime { get; set; }
        public string? Purpose { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Verified { get; set; } = true;
    }

    public class AppointmentBookingResult
    {
        public int RowsAffected { get; set; }
        public string? AppointmentId { get; set; }
        public string? ErrorMessage { get; set; }
        public AppointmentConfirmationQrDto? ConfirmationQr { get; set; }
    }
}
