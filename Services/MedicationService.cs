using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class MedicationService : IMedicationService
    {
        private readonly IMedicationRepository _repository;

        public MedicationService(IMedicationRepository repository)
        {
            _repository = repository;
        }

        public Task<List<CurrentMedicationItem>> GetCurrentMedicationsAsync(string mrNo)
        {
            return _repository.GetCurrentMedicationsAsync(mrNo);
        }

        public Task<MedicationDetailResponse?> GetMedicationDetailAsync(string mrNo, int medicationId)
        {
            return _repository.GetMedicationDetailAsync(mrNo, medicationId);
        }

        public async Task<RefillRequestItem?> CreateRefillRequestAsync(RefillRequestPayload request)
        {
            var medication = await _repository.GetMedicationDetailAsync(request.MrNo, request.MedicationId);
            if (medication == null)
            {
                return null;
            }

            var refillId = await _repository.CreateRefillRequestAsync(request, medication);
            return await _repository.GetRefillRequestAsync(refillId, request.MrNo);
        }

        public Task<RefillRequestItem?> GetRefillRequestAsync(int refillId, string mrNo)
        {
            return _repository.GetRefillRequestAsync(refillId, mrNo);
        }

        public Task<List<RefillRequestItem>> GetRefillRequestsAsync(string mrNo)
        {
            return _repository.GetRefillRequestsAsync(mrNo);
        }
    }
}
