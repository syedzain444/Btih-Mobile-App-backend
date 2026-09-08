using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IPushNotificationRepository
    {
        Task RegisterDeviceTokenAsync(RegisterDeviceTokenRequest request);
        Task UnregisterDeviceTokenAsync(UnregisterDeviceTokenRequest request);
        Task<List<PatientDeviceToken>> GetActiveTokensAsync(string mrNo);
        Task DeactivateTokenAsync(string mrNo, string deviceToken);
    }
}
