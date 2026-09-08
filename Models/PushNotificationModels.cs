namespace HospitalMobileAPPApi.Models
{
    /// <summary>FCM device token registration request.</summary>
    public class RegisterDeviceTokenRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>FCM device token from Firebase SDK.</summary>
        /// <example>fcm-device-token-from-firebase</example>
        public string DeviceToken { get; set; } = string.Empty;

        /// <summary>Device platform (android / ios).</summary>
        /// <example>android</example>
        public string Platform { get; set; } = "android";
    }

    /// <summary>FCM device token removal request (logout).</summary>
    public class UnregisterDeviceTokenRequest
    {
        /// <summary>Patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>FCM device token to remove.</summary>
        /// <example>fcm-device-token-from-firebase</example>
        public string DeviceToken { get; set; } = string.Empty;
    }

    /// <summary>Hospital-triggered push notification payload.</summary>
    public class SendPatientNotificationRequest
    {
        /// <summary>Target patient MR number.</summary>
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>Notification title shown in system tray.</summary>
        /// <example>Appointment Reminder</example>
        public string Title { get; set; } = string.Empty;

        /// <summary>Notification body text.</summary>
        /// <example>Your appointment is tomorrow at 10:00 AM</example>
        public string Body { get; set; } = string.Empty;

        /// <summary>Optional appointment id (appointment-reminder).</summary>
        /// <example>12345</example>
        public string? AppointmentId { get; set; }

        /// <summary>Optional report type: lab, radiology, etc. (report-ready).</summary>
        /// <example>lab</example>
        public string? ReportType { get; set; }

        /// <summary>Optional report id (report-ready).</summary>
        /// <example>67890</example>
        public string? ReportId { get; set; }
    }

    public class PatientDeviceToken
    {
        public string MrNo { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public class PushSendResult
    {
        public int Sent { get; set; }
        public int Failed { get; set; }
        public int TotalTokens { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class FcmSendResult
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class PushNotificationTypes
    {
        public const string ProfileUpdated = "profile_updated";
        public const string AppointmentReminder = "appointment_reminder";
        public const string ReportReady = "report_ready";
    }
}
