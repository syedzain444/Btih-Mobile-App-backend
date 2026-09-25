using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IGuestRepository
    {
        Task<GuestProfileRecord?> GetByMobileAsync(string mobileNumber);
        Task<GuestProfileRecord> UpsertProfileAsync(GuestProfileRequest request);
        Task<GuestAppointmentRecord> InsertAppointmentAsync(GuestBookAppointmentRequest request);
        Task<List<GuestAppointmentRecord>> GetAppointmentsByMobileAsync(string mobileNumber);
        Task<bool> CancelAppointmentAsync(int guestAppointmentId, string mobileNumber, string reason);
    }
}
