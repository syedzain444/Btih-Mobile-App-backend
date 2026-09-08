namespace HospitalMobileAPPApi.Models
{
    /// <summary>Patient profile update payload.</summary>
    public class UpdatePatientProfileRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>First name.</summary>
        /// <example>Ali</example>
        public string? FirstName { get; set; }

        /// <summary>Last name.</summary>
        /// <example>Khan</example>
        public string? LastName { get; set; }

        /// <summary>Gender (M/F or Male/Female).</summary>
        /// <example>M</example>
        public string? Gender { get; set; }

        /// <summary>Date of birth.</summary>
        public DateTime? DateOfBirth { get; set; }

        /// <summary>National ID (CNIC).</summary>
        public string? CNIC { get; set; }

        /// <summary>Contact mobile number.</summary>
        public string? ContactNo { get; set; }

        /// <summary>Blood group.</summary>
        /// <example>O+</example>
        public string? BloodGroup { get; set; }

        /// <summary>Email address.</summary>
        public string? EmailAddress { get; set; }
    }
}
