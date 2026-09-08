namespace HospitalMobileAPPApi.Models
{
    public class PatientDischargeHistory
    {
            public string MR_NO { get; set; }
            public int PATIENT_VISIT_ID { get; set; }
            public DateTime? CHECK_IN { get; set; }
            public DateTime? DR_OUT { get; set; }
            public string DOCTOR_NAME { get; set; }
            public string ADMISSION_OFFICER { get; set; }
    }
}
