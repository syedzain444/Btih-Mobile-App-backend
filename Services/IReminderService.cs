using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IReminderService
    {
        Task<List<MedicationReminderItem>> GetRemindersAsync(string mrNo);
        Task<MedicationReminderItem?> CreateReminderAsync(MedicationReminderRequest request);
        Task<bool> UpdateReminderAsync(int reminderId, UpdateMedicationReminderRequest request);
        Task<bool> DeleteReminderAsync(int reminderId, string mrNo);
        Task ProcessDueRemindersAsync();
    }
}
