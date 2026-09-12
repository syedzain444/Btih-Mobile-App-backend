using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IGuestService
    {
        Task<GuestProfileRecord?> GetProfileByMobileAsync(string mobileNumber);
        Task<GuestProfileRecord> SaveProfileAsync(GuestProfileRequest request);
        Task<List<PatientAppointment>> GetAppointmentsByPhoneAsync(string phoneNumber);
        Task<bool> CancelAppointmentAsync(string appointmentId, string phoneNumber, string reason);
    }
}
