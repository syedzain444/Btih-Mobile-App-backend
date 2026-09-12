using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface ITrustedDeviceRepository
    {
        Task<bool> IsTrustedAsync(string mrNo, string deviceInstallId, string trustTokenHash);

        Task<string> UpsertTrustedDeviceAsync(
            string mrNo,
            string deviceInstallId,
            string trustTokenHash,
            string? deviceLabel,
            string? platform,
            DateTime expiresAt);

        Task TouchTrustedLoginAsync(string mrNo, string deviceInstallId, DateTime expiresAt);

        Task<IReadOnlyList<TrustedDeviceRecord>> GetTrustedDevicesAsync(string mrNo);

        Task<bool> RevokeTrustedDeviceAsync(string mrNo, int trustedDeviceId);

        Task RevokeAllTrustedDevicesAsync(string mrNo);
    }
}
