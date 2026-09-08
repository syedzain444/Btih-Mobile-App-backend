using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class ReminderService : IReminderService
    {
        private readonly IReminderRepository _repository;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(
            IReminderRepository repository,
            IPushNotificationService pushNotificationService,
            ILogger<ReminderService> logger)
        {
            _repository = repository;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        public Task<List<MedicationReminderItem>> GetRemindersAsync(string mrNo)
        {
            return _repository.GetRemindersAsync(mrNo);
        }

        public async Task<MedicationReminderItem?> CreateReminderAsync(MedicationReminderRequest request)
        {
            if (!IsValidReminderTime(request.ReminderTime))
            {
                return null;
            }

            var reminderId = await _repository.CreateReminderAsync(request);
            var reminders = await _repository.GetRemindersAsync(request.MrNo);
            return reminders.FirstOrDefault(r => r.ReminderId == reminderId);
        }

        public Task<bool> UpdateReminderAsync(int reminderId, UpdateMedicationReminderRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ReminderTime) && !IsValidReminderTime(request.ReminderTime))
            {
                return Task.FromResult(false);
            }

            return _repository.UpdateReminderAsync(reminderId, request);
        }

        public Task<bool> DeleteReminderAsync(int reminderId, string mrNo)
        {
            return _repository.DeleteReminderAsync(reminderId, mrNo);
        }

        public async Task ProcessDueRemindersAsync()
        {
            var now = DateTime.Now;
            var currentTime = now.ToString("HH:mm");
            var dayOfWeek = MapDayOfWeek(now.DayOfWeek);

            var dueReminders = await _repository.GetDueRemindersAsync(currentTime, dayOfWeek);

            foreach (var reminder in dueReminders)
            {
                try
                {
                    await _pushNotificationService.SendAppointmentReminderAsync(new SendPatientNotificationRequest
                    {
                        MrNo = reminder.MrNo,
                        Title = "Medication Reminder",
                        Body = $"Time to take {reminder.MedicationName}",
                    });

                    await _repository.MarkReminderTriggeredAsync(reminder.ReminderId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process medication reminder {ReminderId}", reminder.ReminderId);
                }
            }
        }

        private static bool IsValidReminderTime(string reminderTime)
        {
            if (TimeSpan.TryParse(reminderTime, out var parsed))
            {
                return parsed >= TimeSpan.Zero && parsed < TimeSpan.FromHours(24);
            }

            return false;
        }

        private static int MapDayOfWeek(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => 1,
                DayOfWeek.Tuesday => 2,
                DayOfWeek.Wednesday => 3,
                DayOfWeek.Thursday => 4,
                DayOfWeek.Friday => 5,
                DayOfWeek.Saturday => 6,
                DayOfWeek.Sunday => 7,
                _ => 1,
            };
        }
    }
}
