namespace HospitalMobileAPPApi.Models
{
    public class PatientProfile
    {
        public int SerialNumber { get; set; }   // 👈 Add this
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public string? Gender { get; set; }

        public DateTime? VisitDate { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? CNIC { get; set; }

        public string? ContactNo { get; set; }

        public string? BloodGroup { get; set; }
        public string? EmailAddress { get; set; }
        public string? DoctorName { get; set; }

    }
}
