using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IReminderRepository
    {
        Task<List<MedicationReminderItem>> GetRemindersAsync(string mrNo);
        Task<int> CreateReminderAsync(MedicationReminderRequest request);
        Task<bool> UpdateReminderAsync(int reminderId, UpdateMedicationReminderRequest request);
        Task<bool> DeleteReminderAsync(int reminderId, string mrNo);
        Task<List<DueMedicationReminder>> GetDueRemindersAsync(string currentTime, int dayOfWeek);
        Task MarkReminderTriggeredAsync(int reminderId);
    }
}
