namespace HospitalMobileAPPApi.Models
{
    /// <summary>Password reset request after OTP verification.</summary>
    public class UpdatePasswordRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>New password — hospital policy: 8–64 chars with upper, lower, digit, special.</summary>
        /// <example>NewPass@123</example>
        public string PatientPassword { get; set; } = string.Empty;
    }
}
