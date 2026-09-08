using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IMedicationRepository
    {
        Task<List<CurrentMedicationItem>> GetCurrentMedicationsAsync(string mrNo);
        Task<MedicationDetailResponse?> GetMedicationDetailAsync(string mrNo, int medicationId);
        Task<int> CreateRefillRequestAsync(RefillRequestPayload request, CurrentMedicationItem medication);
        Task<RefillRequestItem?> GetRefillRequestAsync(int refillId, string mrNo);
        Task<List<RefillRequestItem>> GetRefillRequestsAsync(string mrNo);
    }
}
