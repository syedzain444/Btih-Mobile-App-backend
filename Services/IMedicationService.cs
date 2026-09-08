using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IMedicationService
    {
        Task<List<CurrentMedicationItem>> GetCurrentMedicationsAsync(string mrNo);
        Task<MedicationDetailResponse?> GetMedicationDetailAsync(string mrNo, int medicationId);
        Task<RefillRequestItem?> CreateRefillRequestAsync(RefillRequestPayload request);
        Task<RefillRequestItem?> GetRefillRequestAsync(int refillId, string mrNo);
        Task<List<RefillRequestItem>> GetRefillRequestsAsync(string mrNo);
    }
}
