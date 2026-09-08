namespace HospitalMobileAPPApi.Models
{
    public class PatientVisitHistoryItem
    {
        public int SerialNumber { get; set; }
        public int PatientVisitId { get; set; }
        public DateTime? VisitDate { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? DischargeDate { get; set; }
        public int? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string? Department { get; set; }
        public string? AdmissionOfficer { get; set; }
        public string? AdmissionNo { get; set; }
        public string? Disease { get; set; }
        public string? PresentingComplaints { get; set; }
        public bool IsDischarged { get; set; }
        public int? DischargeId { get; set; }
    }
}
