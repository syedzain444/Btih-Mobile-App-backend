using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IGuestRepository
    {
        Task<GuestProfileRecord?> GetByMobileAsync(string mobileNumber);
        Task<GuestProfileRecord> UpsertProfileAsync(GuestProfileRequest request);
    }
}
