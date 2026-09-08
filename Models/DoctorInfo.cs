namespace HospitalMobileAPPApi.Models
{
    public class DoctorInfo
    {
        public int SerialNumber { get; set; }   // 👈 Add this
        public int Doctor_ID { get; set; }
        public string? DoctorName { get; set; }
        public string? DoctorDescription { get; set; }
        public string? DoctorImagePath { get; set; }
        public int Department_ID { get; set; }
        public string? SpecializationName { get; set; }
    }
}