using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public interface IAppPinService
    {
        Task<bool> HasActivePinAsync(string mrNo);
        Task UpsertPinAsync(string mrNo, string pinHash, string? deviceLabel);
        Task<bool> ClearPinAsync(string mrNo);
        Task<bool> VerifyPinHashAsync(string mrNo, string pinHash);
    }

    public class AppPinService : IAppPinService
    {
        private readonly IAppPinRepository _repository;

        public AppPinService(IAppPinRepository repository)
        {
            _repository = repository;
        }

        public Task<bool> HasActivePinAsync(string mrNo) =>
            _repository.HasActivePinAsync(mrNo);

        public Task UpsertPinAsync(string mrNo, string pinHash, string? deviceLabel) =>
            _repository.UpsertPinAsync(mrNo, pinHash, deviceLabel);

        public Task<bool> ClearPinAsync(string mrNo) =>
            _repository.ClearPinAsync(mrNo);

        public async Task<bool> VerifyPinHashAsync(string mrNo, string pinHash)
        {
            var stored = await _repository.GetPinHashAsync(mrNo);
            if (string.IsNullOrWhiteSpace(stored) || string.IsNullOrWhiteSpace(pinHash))
            {
                return false;
            }

            return string.Equals(stored.Trim(), pinHash.Trim(), StringComparison.Ordinal);
        }
    }
}
