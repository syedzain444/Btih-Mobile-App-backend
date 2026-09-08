namespace HospitalMobileAPPApi.Models
{
    public class PatientAdmissionResponse
    {
        public string PatientType { get; set; }
        public string PatientTypeCode { get; set; }
        public string AdmissionId { get; set; }
        public string MrNo { get; set; }
        public string PatientName { get; set; }
        public string DoctorName { get; set; }
        public DateTime? DateAdvised { get; set; }
        public DateTime? ProposedAdmissionDate { get; set; }
        public string ContactNo { get; set; }
        public string ServiceName { get; set; }
        public string PresentingComplaints { get; set; }
        public string Instructions { get; set; }
        public string Ward { get; set; }
        public string IsActive { get; set; }
        public string IsAdmitted { get; set; }
        public string Bed { get; set; }
        public string WardAdmitted { get; set; }
        public string Floor { get; set; }
        public string PatientVisitId { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public string AdmissionDateFormatted { get; set; }
        public string IsCleared { get; set; }
        public decimal AdvanceAmount { get; set; }
        public string AdmissionOfficer { get; set; }
        public string AdmissionNo { get; set; }
        public string Detail { get; set; }
        public DateTime? DischargeDate { get; set; }
        public string IsDC { get; set; }
        public DateTime? ClearedDate { get; set; }
        public string DoctorStatus { get; set; }
        public string RoomNo { get; set; }
        public string BedName { get; set; }
        public string DoctorId { get; set; }
        public string BedId { get; set; }
        public string Attendant { get; set; }
    }
}
