namespace HospitalMobileAPPApi.Models
{
    public class VerifyLoginOtpRequest
    {
        public string LoginChallengeId { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty;

        public string DeviceInstallId { get; set; } = string.Empty;

        public string? DeviceLabel { get; set; }

        public string? Platform { get; set; }

        public bool TrustDevice { get; set; } = true;
    }

    public class RevokeTrustedDeviceRequest
    {
        public string MrNo { get; set; } = string.Empty;

        public int TrustedDeviceId { get; set; }
    }

    public class RevokeAllTrustedDevicesRequest
    {
        public string MrNo { get; set; } = string.Empty;
    }

    public class TrustedDeviceRecord
    {
        public int TrustedDeviceId { get; set; }

        public string MrNo { get; set; } = string.Empty;

        public string DeviceInstallId { get; set; } = string.Empty;

        public string? DeviceLabel { get; set; }

        public string? Platform { get; set; }

        public DateTime TrustedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsActive { get; set; }
    }

    public class LoginChallengeCacheEntry
    {
        public string MrNo { get; set; } = string.Empty;

        public string ContactNo { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string DeviceInstallId { get; set; } = string.Empty;

        public string? DeviceLabel { get; set; }

        public string? Platform { get; set; }

        public string Otp { get; set; } = string.Empty;
    }
}
