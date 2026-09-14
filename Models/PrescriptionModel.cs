using System.Text.Json.Serialization;

namespace HospitalMobileAPPApi.Models
{
    public class PrescriptionModel
    {
        [JsonPropertyName("visitDate")]
        public DateTime? VISIT_DATE { get; set; }

        [JsonPropertyName("doctor")]
        public string? DOCTOR { get; set; }

        [JsonPropertyName("patVisitId")]
        public int PAT_VISIT_ID { get; set; }

        [JsonPropertyName("department")]
        public string? DEPARTMENT { get; set; }
    }
}
