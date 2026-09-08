using System.Text.Json.Serialization;

namespace HospitalMobileAPPApi.Models
{
    public class LabReportModel
    {
        [JsonPropertyName("sampleCollectionDate")]
        public DateTime? DT_SAMPLECOLLECTION { get; set; }

        [JsonPropertyName("patDiagId")]
        public int PAT_DIAG_ID { get; set; }

        [JsonPropertyName("modalityName")]
        public string? MODALITY_NM { get; set; }

        [JsonPropertyName("diagnosticName")]
        public string? DIAGNOSTIC_NAME { get; set; }

        [JsonPropertyName("testType")]
        public string? TESTTYPE { get; set; }
    }
}
