namespace HospitalMobileAPPApi.Models
{
    public class PatientProfileResponse
    {
        public PatientDetails Profile { get; set; } = new();
        public PagedResult<PatientVisitHistoryItem> VisitHistory { get; set; } = new();
    }
}
