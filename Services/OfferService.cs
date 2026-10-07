using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public interface IOfferService
    {
        Task<List<MobileOfferRecord>> GetAllAsync();
        Task<List<MobileOfferRecord>> GetActiveAsync();
        Task<MobileOfferRecord?> GetByIdAsync(int offerId);
        Task<MobileOfferRecord> CreateAsync(MobileOfferRecord record);
        Task<bool> UpdateAsync(MobileOfferRecord record);
        Task<bool> DeleteAsync(int offerId);
    }

    public class OfferService : IOfferService
    {
        private readonly IOfferRepository _repository;

        public OfferService(IOfferRepository repository)
        {
            _repository = repository;
        }

        public Task<List<MobileOfferRecord>> GetAllAsync() => _repository.GetAllAsync();

        public Task<List<MobileOfferRecord>> GetActiveAsync() => _repository.GetActiveAsync();

        public Task<MobileOfferRecord?> GetByIdAsync(int offerId) =>
            _repository.GetByIdAsync(offerId);

        public Task<MobileOfferRecord> CreateAsync(MobileOfferRecord record) =>
            _repository.InsertAsync(record);

        public Task<bool> UpdateAsync(MobileOfferRecord record) =>
            _repository.UpdateAsync(record);

        public Task<bool> DeleteAsync(int offerId) =>
            _repository.DeleteAsync(offerId);
    }
}
