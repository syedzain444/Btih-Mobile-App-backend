using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IRecentActivityRepository
    {
        Task<PatientRecentActivityRecord> UpsertAsync(PatientRecentActivityRecord record);
        Task<List<PatientRecentActivityRecord>> GetLatestAsync(string mrNo, int limit);
        Task TrimToLimitAsync(string mrNo, int limit);
        Task<int> ClearAsync(string mrNo);
    }
}
