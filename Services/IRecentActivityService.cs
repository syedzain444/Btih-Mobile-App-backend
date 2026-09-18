using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IRecentActivityService
    {
        Task<PatientRecentActivityRecord> RecordAsync(RecordRecentActivityRequest request);
        Task<List<PatientRecentActivityRecord>> GetLatestAsync(string mrNo, int limit = 3);
        Task<int> ClearAsync(string mrNo);
    }
}
