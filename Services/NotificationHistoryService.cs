using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class NotificationHistoryService : INotificationHistoryService
    {
        private readonly INotificationHistoryRepository _repository;

        public NotificationHistoryService(INotificationHistoryRepository repository)
        {
            _repository = repository;
        }

        public Task<int> RecordNotificationAsync(
            string mrNo,
            string title,
            string body,
            string notificationType,
            Dictionary<string, string>? payload = null,
            string? category = null,
            string? priority = null)
        {
            var record = new PatientNotificationRecord
            {
                MrNo = mrNo.Trim(),
                NotificationType = notificationType.Trim(),
                Category = category ?? ResolveCategory(notificationType, payload),
                Priority = priority ?? ResolvePriority(notificationType),
                Title = title.Trim(),
                Body = body.Trim(),
                PayloadJson = NotificationHistoryRepository.SerializePayload(payload),
            };

            return _repository.InsertAsync(record);
        }

        public Task<NotificationInboxResult> GetInboxAsync(
            string mrNo,
            int pageNumber,
            int pageSize,
            string? category)
        {
            return _repository.GetInboxAsync(mrNo.Trim(), pageNumber, pageSize, category);
        }

        public Task<int> GetUnreadCountAsync(string mrNo)
        {
            return _repository.GetUnreadCountAsync(mrNo.Trim());
        }

        public Task<bool> MarkAsReadAsync(int notificationId, string mrNo)
        {
            return _repository.MarkAsReadAsync(notificationId, mrNo.Trim());
        }

        public Task<int> MarkAllAsReadAsync(string mrNo)
        {
            return _repository.MarkAllAsReadAsync(mrNo.Trim());
        }

        public static string ResolveCategory(
            string notificationType,
            Dictionary<string, string>? payload)
        {
            if (payload != null &&
                payload.TryGetValue("reportType", out var reportType) &&
                !string.IsNullOrWhiteSpace(reportType))
            {
                var normalized = reportType.Trim().ToLowerInvariant();
                if (normalized.Contains("lab"))
                {
                    return "lab";
                }

                if (normalized.Contains("gastro") || normalized.Contains("radio"))
                {
                    return "records";
                }
            }

            if (payload != null &&
                payload.TryGetValue("screen", out var screen) &&
                !string.IsNullOrWhiteSpace(screen))
            {
                return screen.Trim().ToLowerInvariant() switch
                {
                    "appointments" => "appointments",
                    "reports" => "records",
                    "billing" => "billing",
                    _ => "general",
                };
            }

            return notificationType.Trim().ToLowerInvariant() switch
            {
                PushNotificationTypes.AppointmentReminder => "appointments",
                PushNotificationTypes.ReportReady => "records",
                PushNotificationTypes.ProfileUpdated => "general",
                "medication_reminder" => "general",
                _ => "general",
            };
        }

        public static string ResolvePriority(string notificationType)
        {
            return notificationType.Trim().ToLowerInvariant() switch
            {
                PushNotificationTypes.AppointmentReminder => "high",
                PushNotificationTypes.ReportReady => "high",
                _ => "normal",
            };
        }
    }
}
