namespace HospitalMobileAPPApi.Configuration
{
    public class MessagingSettings
    {
        public const string SectionName = "Messaging";
        public string UploadSubPath { get; set; } = "uploads/messages";
        public long MaxAttachmentBytes { get; set; } = 10485760;
        public string AllowedExtensions { get; set; } = ".pdf,.jpg,.jpeg,.png,.gif,.webp";
    }

    public class ReminderSettings
    {
        public const string SectionName = "MedicationReminders";
        public bool EnableServerPush { get; set; } = true;
        public int PollIntervalSeconds { get; set; } = 60;
    }
}
