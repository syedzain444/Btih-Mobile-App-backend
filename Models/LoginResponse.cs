namespace HospitalMobileAPPApi.Models
{
    public class LoginResponse
    {
        public string? MrNo { get; set; }
        public string? FirstName { get; set; }
        /// <summary>Registered mobile used for OTP / trusted-device SMS.</summary>
        public string? ContactNo { get; set; }
    }
}
