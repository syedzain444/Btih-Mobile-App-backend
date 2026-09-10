using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IRegistrationService
    {
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<bool> CompleteProfileSetupAsync(ProfileSetupRequest request);
        Task<PatientDetails?> GetPatientDetailsAsync(string mrNo);
        Task<bool> HasPortalAccountAsync(string phoneNumber, string mrNo);
    }
}
