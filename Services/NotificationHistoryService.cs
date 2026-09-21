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
                NotificationType = notificationType.Trim().ToLowerInvariant(),
                Category = (category ?? ResolveCategory(notificationType, payload)).Trim().ToLowerInvariant(),
                Priority = (priority ?? ResolvePriority(notificationType)).Trim().ToLowerInvariant(),
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
                payload.TryGetValue("category", out var explicitCategory) &&
                !string.IsNullOrWhiteSpace(explicitCategory))
            {
                return explicitCategory.Trim().ToLowerInvariant();
            }

            if (payload != null &&
                payload.TryGetValue("reportType", out var reportType) &&
                !string.IsNullOrWhiteSpace(reportType))
            {
                var normalized = reportType.Trim().ToLowerInvariant();
                if (normalized.Contains("lab"))
                {
                    return NotificationCategories.Lab;
                }

                if (normalized.Contains("gastro") ||
                    normalized.Contains("radio") ||
                    normalized.Contains("discharge"))
                {
                    return NotificationCategories.Records;
                }
            }

            var type = notificationType.Trim().ToLowerInvariant().Replace('-', '_');

            return type switch
            {
                PushNotificationTypes.AppointmentRequestReceived or
                PushNotificationTypes.AppointmentConfirmed or
                PushNotificationTypes.AppointmentCancelled or
                PushNotificationTypes.AppointmentRescheduled or
                PushNotificationTypes.AppointmentReminder or
                PushNotificationTypes.FollowUpReminder
                    => NotificationCategories.Appointments,

                PushNotificationTypes.LabReportReady
                    => NotificationCategories.Lab,

                PushNotificationTypes.GastroReportReady or
                PushNotificationTypes.RadiologyReportReady or
                PushNotificationTypes.DischargeSummaryReady or
                PushNotificationTypes.VisitSummaryReady or
                PushNotificationTypes.ReportReady
                    => NotificationCategories.Records,

                PushNotificationTypes.PrescriptionAdded or
                PushNotificationTypes.MedicationReminder or
                PushNotificationTypes.MedicationScheduleUpdated
                    => NotificationCategories.Medications,

                PushNotificationTypes.BillGenerated or
                PushNotificationTypes.PaymentPending or
                PushNotificationTypes.PaymentConfirmed
                    => NotificationCategories.Billing,

                PushNotificationTypes.MessageReceived or
                PushNotificationTypes.MessageThreadClosed
                    => NotificationCategories.Messaging,

                PushNotificationTypes.ProfileUpdated or
                PushNotificationTypes.PasswordChanged or
                PushNotificationTypes.AppPinChanged or
                PushNotificationTypes.TrustedDeviceAdded or
                PushNotificationTypes.TrustedDeviceRemoved or
                PushNotificationTypes.NewLoginAlert
                    => NotificationCategories.Security,

                PushNotificationTypes.HospitalAnnouncement or
                PushNotificationTypes.HospitalPromotion
                    => NotificationCategories.General,

                _ when type.Contains("appointment") || type.Contains("follow")
                    => NotificationCategories.Appointments,
                _ when type.Contains("medication") || type.Contains("prescription")
                    => NotificationCategories.Medications,
                _ when type.Contains("lab")
                    => NotificationCategories.Lab,
                _ when type.Contains("bill") || type.Contains("payment")
                    => NotificationCategories.Billing,
                _ when type.Contains("message")
                    => NotificationCategories.Messaging,
                _ when type.Contains("password") || type.Contains("pin") ||
                       type.Contains("trusted") || type.Contains("login") ||
                       type.Contains("profile")
                    => NotificationCategories.Security,
                _ => NotificationCategories.General,
            };
        }

        public static string ResolvePriority(string notificationType)
        {
            var type = notificationType.Trim().ToLowerInvariant().Replace('-', '_');
            return type switch
            {
                PushNotificationTypes.AppointmentReminder or
                PushNotificationTypes.AppointmentCancelled or
                PushNotificationTypes.LabReportReady or
                PushNotificationTypes.MedicationReminder or
                PushNotificationTypes.PaymentPending or
                PushNotificationTypes.MessageReceived or
                PushNotificationTypes.NewLoginAlert or
                PushNotificationTypes.PasswordChanged
                    => "high",

                PushNotificationTypes.HospitalPromotion or
                PushNotificationTypes.MessageThreadClosed
                    => "low",

                _ => "normal",
            };
        }
    }
}
