namespace HospitalMobileAPPApi.Configuration
{
    public class SmsSettings
    {
        public const string SectionName = "Sms";
        public string BaseUrl { get; set; } = "http://172.20.10.50:81/api/values/?_Send_to=";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class AuthSettings
    {
        public const string SectionName = "Auth";
        public int OtpExpiryMinutes { get; set; } = 2;
        public int PasswordResetWindowMinutes { get; set; } = 5;
    }

    public class SecuritySettings
    {
        public const string SectionName = "Security";
        public int MobileAppEmpId { get; set; } = 1;
        public bool RequireHttps { get; set; } = true;
    }

    public class RateLimitSettings
    {
        public const string SectionName = "RateLimiting";
        public int PermitLimit { get; set; } = 120;
        public int WindowSeconds { get; set; } = 60;
    }
}
