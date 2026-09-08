namespace HospitalMobileAPPApi.Models
{
    public class CurrentMedicationItem
    {
        public int MedicationId { get; set; }
        public int? PpId { get; set; }
        public int? MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? DoseWhen { get; set; }
        public decimal? Days { get; set; }
        public decimal? PerDay { get; set; }
        public string? Frequency { get; set; }
        public string? Route { get; set; }
        public string? Remarks { get; set; }
        public string? Doctor { get; set; }
        public string? Department { get; set; }
        public int? PatientVisitId { get; set; }
        public DateTime? VisitDate { get; set; }
    }

    public class MedicationDetailResponse : CurrentMedicationItem
    {
        public decimal? Quantity { get; set; }
        public string? PharmacyName { get; set; }
    }

    public class RefillRequestPayload
    {
        public string MrNo { get; set; } = string.Empty;
        public int MedicationId { get; set; }
        public int? Quantity { get; set; }
        public string? Notes { get; set; }
    }

    public class RefillRequestItem
    {
        public int RefillId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public int? MedicationId { get; set; }
        public string? MedicationName { get; set; }
        public int? PpId { get; set; }
        public int? PatientVisitId { get; set; }
        public int? Quantity { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StatusMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
