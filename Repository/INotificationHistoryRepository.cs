using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface INotificationHistoryRepository
    {
        Task<int> InsertAsync(PatientNotificationRecord record);
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
