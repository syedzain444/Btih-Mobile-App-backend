namespace HospitalMobileAPPApi.Models
{
    public class PatientAppointment
    {
        public string AppointmentId { get; set; }
        public string Name { get; set; }
        public string PhoneNo { get; set; }
        public string MRNo { get; set; }
        public string Email { get; set; }

        public int weekId { get; set; }

        public string AppointmentTime { get; set; }
        public string Status { get; set; }
        public string DoctorName { get; set; }
        public int DoctorId { get; set; }
        public int DepartmentId { get; set; }
        public string purpose { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
