using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IRegistrationRepository
    {
        Task<bool> HasPortalPasswordAsync(string mrNo);
        Task<bool> IsPhoneRegisteredAsync(string contactNo);
        Task<bool> IsMobilePhoneRegisteredAsync(string contactNo);
        Task<MobilePatientRecord?> GetMobilePatientByPhoneAsync(string contactNo);
        Task<MobilePatientRecord?> GetMobilePatientByMrNoAsync(string mrNo);
        Task<string> CreateMobilePatientAsync(string contactNo, string firstName, string? lastName, string password, bool acceptedTerms);
        Task<bool> UpdateHmisPatientNamesAsync(string mrNo, string? firstName, string? lastName);
        Task<bool> CompleteMobileProfileSetupAsync(ProfileSetupRequest request);
        Task<bool> IsProfileSetupRequiredAsync(string mrNo);
        Task<PatientDetails?> GetMobilePatientDetailsAsync(string mrNo);
    }
}
