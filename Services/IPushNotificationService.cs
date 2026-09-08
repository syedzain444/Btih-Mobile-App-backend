using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IPushNotificationService
    {
        Task RegisterDeviceTokenAsync(RegisterDeviceTokenRequest request);
        Task UnregisterDeviceTokenAsync(UnregisterDeviceTokenRequest request);
        Task<PushSendResult> SendToPatientAsync(
            string mrNo,
            string title,
            string body,
            string notificationType,
            Dictionary<string, string>? data = null);
        Task<PushSendResult> SendProfileUpdatedNotificationAsync(string mrNo, string? firstName);
        Task<PushSendResult> SendAppointmentReminderAsync(SendPatientNotificationRequest request);
        Task<PushSendResult> SendReportReadyNotificationAsync(SendPatientNotificationRequest request);
    }
}
