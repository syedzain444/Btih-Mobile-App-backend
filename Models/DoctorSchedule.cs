namespace HospitalMobileAPPApi.Models
{
    public class DoctorSchedule
    {
        public int SerialNumber { get; set; }   // 👈 Add this
        public int Doctor_ID { get; set; }
        public string? DoctorName { get; set; }
        public string? DayName { get; set; }
        public DateTime? TimeFrom { get; set; }
        public DateTime? TimeTo { get; set; }
        public int Week_ID { get; set; }
        public int OPD_Charges { get; set; }


    }
}
