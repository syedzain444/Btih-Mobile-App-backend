using System.Text.Json.Serialization;

namespace HospitalMobileAPPApi.Models
{
    public class PatientDischargeHistory
    {
        [JsonPropertyName("mrNo")]
        public string MR_NO { get; set; } = string.Empty;

        [JsonPropertyName("patientVisitId")]
        public int PATIENT_VISIT_ID { get; set; }

        [JsonPropertyName("checkIn")]
        public DateTime? CHECK_IN { get; set; }

        [JsonPropertyName("drOut")]
        public DateTime? DR_OUT { get; set; }

        [JsonPropertyName("doctorName")]
        public string DOCTOR_NAME { get; set; } = string.Empty;

        [JsonPropertyName("admissionOfficer")]
        public string ADMISSION_OFFICER { get; set; } = string.Empty;
    }
}
