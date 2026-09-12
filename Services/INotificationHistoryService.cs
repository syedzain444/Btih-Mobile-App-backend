using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface INotificationHistoryService
    {
        Task<int> RecordNotificationAsync(
            string mrNo,
            string title,
            string body,
            string notificationType,
            Dictionary<string, string>? payload = null,
            string? category = null,
            string? priority = null);

        Task<NotificationInboxResult> GetInboxAsync(
            string mrNo,
            int pageNumber,
            int pageSize,
            string? category);

        Task<int> GetUnreadCountAsync(string mrNo);
        Task<bool> MarkAsReadAsync(int notificationId, string mrNo);
        Task<int> MarkAllAsReadAsync(string mrNo);
    }
}
