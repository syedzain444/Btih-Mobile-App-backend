namespace HospitalMobileAPPApi.Models
{
    /// <summary>FCM device token registration request.</summary>
    public class RegisterDeviceTokenRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
        public string Platform { get; set; } = "android";
    }

    public class UnregisterDeviceTokenRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
    }

    /// <summary>Hospital-triggered push notification payload.</summary>
    public class SendPatientNotificationRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? AppointmentId { get; set; }
        public string? ReportType { get; set; }
        public string? ReportId { get; set; }
    }

    /// <summary>Generic typed notification send — persists to PATIENT_NOTIFICATION + FCM.</summary>
    public class SendTypedNotificationRequest
    {
        public string MrNo { get; set; } = string.Empty;

        /// <summary>Wire type, e.g. appointment_confirmed, lab_report_ready.</summary>
        public string NotificationType { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        /// <summary>Optional override; otherwise resolved from NotificationType.</summary>
        public string? Category { get; set; }

        /// <summary>Optional override: high | normal | low.</summary>
        public string? Priority { get; set; }

        public Dictionary<string, string>? Payload { get; set; }
    }

    public class PatientDeviceToken
    {
        public string MrNo { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
    }

    public class PatientDeviceListRequest
    {
        public string MrNo { get; set; } = string.Empty;
    }

    public class UnregisterAllDeviceTokensRequest
    {
        public string MrNo { get; set; } = string.Empty;
    }

    public class PushSendResult
    {
        public int Sent { get; set; }
        public int Failed { get; set; }
        public int TotalTokens { get; set; }
        public int? NotificationId { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class FcmSendResult
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Canonical notification type keys stored in PATIENT_NOTIFICATION.</summary>
    public static class PushNotificationTypes
    {
        // Appointments
        public const string AppointmentRequestReceived = "appointment_request_received";
        public const string AppointmentConfirmed = "appointment_confirmed";
        public const string AppointmentCancelled = "appointment_cancelled";
        public const string AppointmentRescheduled = "appointment_rescheduled";
        public const string AppointmentReminder = "appointment_reminder";
        public const string FollowUpReminder = "follow_up_reminder";

        // Reports / records
        public const string LabReportReady = "lab_report_ready";
        public const string GastroReportReady = "gastro_report_ready";
        public const string RadiologyReportReady = "radiology_report_ready";
        public const string PrescriptionAdded = "prescription_added";
        public const string DischargeSummaryReady = "discharge_summary_ready";
        public const string VisitSummaryReady = "visit_summary_ready";
        public const string ReportReady = "report_ready"; // legacy generic

        // Medications
        public const string MedicationReminder = "medication_reminder";
        public const string MedicationScheduleUpdated = "medication_schedule_updated";

        // Billing
        public const string BillGenerated = "bill_generated";
        public const string PaymentPending = "payment_pending";
        public const string PaymentConfirmed = "payment_confirmed";

        // Messaging
        public const string MessageReceived = "message_received";
        public const string MessageThreadClosed = "message_thread_closed";

        // Security / profile
        public const string ProfileUpdated = "profile_updated";
        public const string PasswordChanged = "password_changed";
        public const string AppPinChanged = "app_pin_changed";
        public const string TrustedDeviceAdded = "trusted_device_added";
        public const string TrustedDeviceRemoved = "trusted_device_removed";
        public const string NewLoginAlert = "new_login_alert";

        // General
        public const string HospitalAnnouncement = "hospital_announcement";
        public const string HospitalPromotion = "hospital_promotion";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            AppointmentRequestReceived, AppointmentConfirmed, AppointmentCancelled,
            AppointmentRescheduled, AppointmentReminder, FollowUpReminder,
            LabReportReady, GastroReportReady, RadiologyReportReady, PrescriptionAdded,
            DischargeSummaryReady, VisitSummaryReady, ReportReady,
            MedicationReminder, MedicationScheduleUpdated,
            BillGenerated, PaymentPending, PaymentConfirmed,
            MessageReceived, MessageThreadClosed,
            ProfileUpdated, PasswordChanged, AppPinChanged,
            TrustedDeviceAdded, TrustedDeviceRemoved, NewLoginAlert,
            HospitalAnnouncement, HospitalPromotion,
        };
    }

    public static class NotificationCategories
    {
        public const string Appointments = "appointments";
        public const string Medications = "medications";
        public const string Lab = "lab";
        public const string Records = "records";
        public const string Billing = "billing";
        public const string Messaging = "messaging";
        public const string Security = "security";
        public const string General = "general";
    }
}
