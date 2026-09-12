using System.Security.Cryptography;
using System.Text;
using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Services
{
    public class TrustedDeviceService : ITrustedDeviceService
    {
        private readonly ITrustedDeviceRepository _repository;
        private readonly AuthSettings _authSettings;
        private readonly string _tokenPepper;

        public TrustedDeviceService(
            ITrustedDeviceRepository repository,
            IOptions<AuthSettings> authSettings,
            IConfiguration configuration)
        {
            _repository = repository;
            _authSettings = authSettings.Value;
            _tokenPepper = configuration["JwtSettings:SecretKey"] ?? "trusted-device-pepper";
        }

        public async Task<bool> IsTrustedDeviceAsync(
            string mrNo,
            string deviceInstallId,
            string? deviceTrustToken)
        {
            if (string.IsNullOrWhiteSpace(deviceInstallId) ||
                string.IsNullOrWhiteSpace(deviceTrustToken))
            {
                return false;
            }

            var hash = HashTrustToken(deviceTrustToken.Trim());
            var trusted = await _repository.IsTrustedAsync(
                mrNo.Trim(),
                deviceInstallId.Trim(),
                hash);

            if (trusted)
            {
                await RecordTrustedLoginAsync(mrNo, deviceInstallId);
            }

            return trusted;
        }

        public async Task<string> RegisterTrustedDeviceAsync(
            string mrNo,
            string deviceInstallId,
            string? deviceLabel,
            string? platform)
        {
            var token = GenerateTrustToken();
            var hash = HashTrustToken(token);
            var expiresAt = DateTime.UtcNow.AddDays(_authSettings.TrustedDeviceDays);

            await _repository.UpsertTrustedDeviceAsync(
                mrNo.Trim(),
                deviceInstallId.Trim(),
                hash,
                deviceLabel,
                platform,
                expiresAt);

            return token;
        }

        public Task RecordTrustedLoginAsync(string mrNo, string deviceInstallId)
        {
            var expiresAt = DateTime.UtcNow.AddDays(_authSettings.TrustedDeviceDays);
            return _repository.TouchTrustedLoginAsync(
                mrNo.Trim(),
                deviceInstallId.Trim(),
                expiresAt);
        }

        public Task<IReadOnlyList<TrustedDeviceRecord>> GetTrustedDevicesAsync(string mrNo)
        {
            return _repository.GetTrustedDevicesAsync(mrNo.Trim());
        }

        public Task<bool> RevokeTrustedDeviceAsync(string mrNo, int trustedDeviceId)
        {
            return _repository.RevokeTrustedDeviceAsync(mrNo.Trim(), trustedDeviceId);
        }

        public Task RevokeAllTrustedDevicesAsync(string mrNo)
        {
            return _repository.RevokeAllTrustedDevicesAsync(mrNo.Trim());
        }

        public static string GenerateTrustToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        private string HashTrustToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{_tokenPepper}:{token}"));
            return Convert.ToHexString(bytes);
        }
    }
}
