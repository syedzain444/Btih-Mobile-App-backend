
using System.Text.Json.Serialization;

namespace HospitalMobileAPPApi.Models
{

    public class PatientAdmission
    {
        // Core Properties with proper JSON property names
        [JsonPropertyName("DOCTOR_ID")]
        public int DOCTOR_ID { get; set; }

        [JsonPropertyName("ADMITTING_SER_ID")]
        public int ADMITTING_SER_ID { get; set; }

        [JsonPropertyName("PRESENTING_COMPLAINTS")]
        public string PRESENTING_COMPLAINTS { get; set; }

        [JsonPropertyName("INSTRUCTIONS")]
        public string INSTRUCTIONS { get; set; }

        [JsonPropertyName("ADMIT_WARD")]
        public int ADMIT_WARD { get; set; }

        [JsonPropertyName("DT_PROPOSED_ADMISSION")]
        public DateTime? DT_PROPOSED_ADMISSION { get; set; }

        [JsonPropertyName("ESTIMATE_LOS")]
        public int ESTIMATE_LOS { get; set; }

        [JsonPropertyName("DT_CREATED")]
        public DateTime? DT_CREATED { get; set; }

        [JsonPropertyName("CREATED_BY")]
        public int CREATED_BY { get; set; }

        [JsonPropertyName("PATIENT_VISIT_ID")]
        public long PATIENT_VISIT_ID { get; set; }

        // Additional Properties
        [JsonPropertyName("ADMISSION_ID")]
        public int ADMISSION_ID { get; set; }

        [JsonPropertyName("MR_NO")]
        public string MR_NO { get; set; }

        [JsonPropertyName("BED_ID")]
        public long BED_ID { get; set; }

        [JsonPropertyName("ADVANCE_AMOUNT")]
        public long ADVANCE_AMOUNT { get; set; }

        [JsonPropertyName("IS_ADMITTED")]
        public string IS_ADMITTED { get; set; }

        [JsonPropertyName("IS_ACTIVE")]
        public string IS_ACTIVE { get; set; }

        [JsonPropertyName("IS_CLEARED")]
        public string IS_CLEARED { get; set; }

        [JsonPropertyName("DC_ID")]
        public long DC_ID { get; set; }

        [JsonPropertyName("ADMISSION_NO")]
        public string ADMISSION_NO { get; set; }

        [JsonPropertyName("CONDITION")]
        public string CONDITION { get; set; }

        // Debug Parameters
        [JsonPropertyName("ADMISSION_DATE")]
        public DateTime? ADMISSION_DATE { get; set; }

        [JsonPropertyName("BRANCH")]
        public int Branch { get; set; }

        [JsonPropertyName("DPT")]
        public string DPT { get; set; }

        [JsonPropertyName("DT_CANCEL")]
        public DateTime? DT_CANCEL { get; set; }

        [JsonPropertyName("AO_ID")]
        public int AO_ID { get; set; }

        [JsonPropertyName("DISEASE")]
        public string Disease { get; set; }

        [JsonPropertyName("IS_DC")]
        public string IS_DC { get; set; }  // Changed from char to string

        [JsonPropertyName("NEXT_OF_KIN")]
        public string NEXT_OF_KIN { get; set; }

        [JsonPropertyName("NOK_CONTACT")]
        public string NOK_CONTACT { get; set; }

        [JsonPropertyName("NOK_RELATION")]
        public string NOK_RELATION { get; set; }

        [JsonPropertyName("ADMISSION_TYPE")]
        public int ADMISSION_TYPE { get; set; }

        [JsonPropertyName("PANEL")]
        public int PANEL { get; set; }

        [JsonPropertyName("PANEL_CODE")]
        public string PANEL_CODE { get; set; }

        [JsonPropertyName("shifting_notes")]
        public string shifting_notes { get; set; }

        // Cancellation Properties
        [JsonPropertyName("CANCEL_BY")]
        public long CANCEL_BY { get; set; }

        [JsonPropertyName("CANCEL_REASON")]
        public string CANCEL_REASON { get; set; }

        // Surgery/Procedure Properties
        [JsonPropertyName("PROC_MST_ID")]
        public long PROC_MST_ID { get; set; }

        [JsonPropertyName("ANES_ID")]
        public long ANES_ID { get; set; }

        [JsonPropertyName("STM_ID")]
        public long STM_ID { get; set; }

        [JsonPropertyName("OT_DURATION")]
        public string OT_DURATION { get; set; }

        // Constructor with default values for debug parameters
        public PatientAdmission()
        {
            Branch = 42;      // Default Branch = 42
            DPT = "IPD";      // Default DPT = IPD
            IS_ACTIVE = "Y";  // Default active status
            IS_ADMITTED = "N";
            IS_CLEARED = "N";
            IS_DC = "N";
        }
    }

    //public class PatientAdmission
    //{
    //    // Core Properties
    //    public int DOCTOR_ID { get; set; }
    //    public int ADMITTING_SER_ID { get; set; }
    //    public string PRESENTING_COMPLAINTS { get; set; }
    //    public string INSTRUCTIONS { get; set; }
    //    public int ADMIT_WARD { get; set; }
    //    public DateTime? DT_PROPOSED_ADMISSION { get; set; }  // 'ANY DATE' support
    //    public int ESTIMATE_LOS { get; set; }
    //    public DateTime? DT_CREATED { get; set; }  // TODAY DATE support
    //    public int CREATED_BY { get; set; }
    //    public long PATIENT_VISIT_ID { get; set; }

    //    // Additional Properties from Windows Form
    //    public int ADMISSION_ID { get; set; }
    //    public string MR_NO { get; set; }
    //    public long BED_ID { get; set; }
    //    public long ADVANCE_AMOUNT { get; set; }
    //    public string IS_ADMITTED { get; set; }
    //    public string IS_ACTIVE { get; set; }
    //    public string IS_CLEARED { get; set; }
    //    public long DC_ID { get; set; }
    //    public string ADMISSION_NO { get; set; }
    //    public string CONDITION { get; set; }

    //    // Debug Parameters (from your debug output)
    //    public DateTime? ADMISSION_DATE { get; set; }  // ADMISSTION_DATE
    //    public int Branch { get; set; }  // Branch = 42 (default)
    //    public string DPT { get; set; }  // DPT = IPD (default)
    //    public DateTime? DT_CANCEL { get; set; }  // DT_CANCEL = 'ANY DATE'

    //    // Additional Properties
    //    public int AO_ID { get; set; }
    //    public string Disease { get; set; }
    //    public char IS_DC { get; set; }
    //    public string NEXT_OF_KIN { get; set; }
    //    public string NOK_CONTACT { get; set; }
    //    public string NOK_RELATION { get; set; }
    //    public int ADMISSION_TYPE { get; set; }
    //    public int PANEL { get; set; }
    //    public string PANEL_CODE { get; set; }
    //    public string shifting_notes { get; set; }

    //    // Cancellation Properties
    //    public long CANCEL_BY { get; set; }
    //    public string CANCEL_REASON { get; set; }

    //    // Surgery/Procedure Properties
    //    public long PROC_MST_ID { get; set; }
    //    public long ANES_ID { get; set; }
    //    public long STM_ID { get; set; }
    //    public string OT_DURATION { get; set; }
    //}
}
