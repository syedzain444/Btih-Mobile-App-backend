namespace HospitalMobileAPPApi.Configuration
{
    public class PaymentGatewaySettings
    {
        public const string SectionName = "PaymentGateway";
        public string Provider { get; set; } = "Mock";
        public string BaseCheckoutUrl { get; set; } = "https://pay.btkhospital.com/checkout";
        public string WebhookSecret { get; set; } = string.Empty;
        public string DefaultReturnUrl { get; set; } = "btihapp://payment/return";
        public int PaymentExpiryMinutes { get; set; } = 30;
        public bool AllowMockConfirm { get; set; } = true;
    }

    public class AdminSettings
    {
        public const string SectionName = "Admin";
        public string PasswordSalt { get; set; } = "BTIH_MOBILE_ADMIN";
        public string BootstrapSecret { get; set; } = string.Empty;
        public int TokenExpiryMinutes { get; set; } = 480;
    }

    public class TelemedicineSettings
    {
        public const string SectionName = "Telemedicine";
        public string Provider { get; set; } = "Internal";
        public int SessionExpiryMinutes { get; set; } = 60;
        public string PatientJoinBaseUrl { get; set; } = "btihapp://telemed/join";
    }

    public class SupportSettings
    {
        public const string SectionName = "Support";
        public string HospitalName { get; set; } = "Bahria Town International Hospital";
        public string Phone { get; set; } = "051-111-111-111";
        public string Email { get; set; } = "it@btkhospital.com";
        public string Address { get; set; } = "Bahria Town, Rawalpindi";
        public string WorkingHours { get; set; } = "24/7 Emergency | OPD 8:00 AM – 8:00 PM";
    }

    public class MonitoringSettings
    {
        public const string SectionName = "Monitoring";
        public bool EnableSerilogFile { get; set; } = true;
        public string LogFilePath { get; set; } = "logs/bti-hospital-api-.log";
        public bool EnableHealthEndpoint { get; set; } = true;
    }
}
