using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface ITrustedDeviceService
    {
        Task<bool> IsTrustedDeviceAsync(
            string mrNo,
            string deviceInstallId,
            string? deviceTrustToken);

        Task<string> RegisterTrustedDeviceAsync(
            string mrNo,
            string deviceInstallId,
            string? deviceLabel,
            string? platform);

        Task RecordTrustedLoginAsync(string mrNo, string deviceInstallId);

        Task<IReadOnlyList<TrustedDeviceRecord>> GetTrustedDevicesAsync(string mrNo);

        Task<bool> RevokeTrustedDeviceAsync(string mrNo, int trustedDeviceId);

        Task RevokeAllTrustedDevicesAsync(string mrNo);
    }
}
