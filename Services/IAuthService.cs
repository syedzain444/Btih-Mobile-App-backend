using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(string contactNo, string password);
        Task<(string? MR_NO, string? CONTACT_NO)> VerifyPhoneNo(string? contactNo, string? mrno);
        Task<string?> GetMrNoByContactNoAsync(string contactNo);
        Task<bool> ContactBelongsToMrNoAsync(string contactNo, string mrNo);
    }
}
