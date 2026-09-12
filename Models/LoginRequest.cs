namespace HospitalMobileAPPApi.Models
{
    /// <summary>Patient login credentials.</summary>
    public class LoginRequest
    {
        /// <summary>Registered mobile number (e.g. 03001234567).</summary>
        /// <example>03001234567</example>
        public string ContactNo { get; set; } = string.Empty;

        /// <summary>Patient portal password.</summary>
        /// <example>yourPassword</example>
        public string Password { get; set; } = string.Empty;

        /// <summary>Stable client install UUID stored in secure storage.</summary>
        public string? DeviceInstallId { get; set; }

        /// <summary>Server-issued trust token from a prior OTP verification.</summary>
        public string? DeviceTrustToken { get; set; }

        public string? DeviceLabel { get; set; }

        public string? Platform { get; set; }
    }
}
