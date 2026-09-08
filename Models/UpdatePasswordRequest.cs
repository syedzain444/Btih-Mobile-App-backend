namespace HospitalMobileAPPApi.Models
{
    /// <summary>Password reset request after OTP verification.</summary>
    public class UpdatePasswordRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>New password (minimum 6 characters).</summary>
        /// <example>newSecurePassword123</example>
        public string PatientPassword { get; set; } = string.Empty;
    }
}
