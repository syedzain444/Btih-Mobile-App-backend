namespace HospitalMobileAPPApi.Models
{
    /// <summary>Logged-in patient password change (current + new).</summary>
    public class ChangePasswordRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string ContactNo { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
