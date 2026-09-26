using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public interface IPromotionService
    {
        Task<List<MobilePromotionRecord>> GetAllAsync();
        Task<List<MobilePromotionRecord>> GetActiveAsync();
        Task<MobilePromotionRecord?> GetByIdAsync(int promotionId);
        Task<MobilePromotionRecord> CreateAsync(MobilePromotionRecord record);
        Task<bool> UpdateAsync(MobilePromotionRecord record);
        Task<bool> DeleteAsync(int promotionId);
    }

    public class PromotionService : IPromotionService
    {
        private readonly IPromotionRepository _repository;

        public PromotionService(IPromotionRepository repository)
        {
            _repository = repository;
        }

        public Task<List<MobilePromotionRecord>> GetAllAsync() => _repository.GetAllAsync();

        public Task<List<MobilePromotionRecord>> GetActiveAsync() => _repository.GetActiveAsync();

        public Task<MobilePromotionRecord?> GetByIdAsync(int promotionId) =>
            _repository.GetByIdAsync(promotionId);

        public Task<MobilePromotionRecord> CreateAsync(MobilePromotionRecord record) =>
            _repository.InsertAsync(record);

        public Task<bool> UpdateAsync(MobilePromotionRecord record) =>
            _repository.UpdateAsync(record);

        public Task<bool> DeleteAsync(int promotionId) =>
            _repository.DeleteAsync(promotionId);
    }
}
