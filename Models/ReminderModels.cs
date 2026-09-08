namespace HospitalMobileAPPApi.Models
{
    public class MedicationReminderRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public int? MedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string ReminderTime { get; set; } = string.Empty;
        public string? DaysOfWeek { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class UpdateMedicationReminderRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string? MedicationName { get; set; }
        public string? ReminderTime { get; set; }
        public string? DaysOfWeek { get; set; }
        public bool? IsEnabled { get; set; }
    }

    public class MedicationReminderItem
    {
        public int ReminderId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public int? MedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string ReminderTime { get; set; } = string.Empty;
        public string DaysOfWeek { get; set; } = "1234567";
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DueMedicationReminder
    {
        public int ReminderId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public string MedicationName { get; set; } = string.Empty;
        public string ReminderTime { get; set; } = string.Empty;
    }
}
