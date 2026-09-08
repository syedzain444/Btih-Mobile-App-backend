using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _repo;

        public AuthService(IAuthRepository repo)
        {
            _repo = repo;
        }

        public Task<LoginResponse?> LoginAsync(string contactNo, string password)
        {
            // Business rules go here later
            return _repo.LoginAsync(contactNo, password);
        }
        public Task<(string? MR_NO, string? CONTACT_NO)> VerifyPhoneNo(string? contactNo, string? mrno)
        {
            return _repo.VerifyPhoneNo(contactNo, mrno);
        }

        public Task<string?> GetMrNoByContactNoAsync(string contactNo)
        {
            return _repo.GetMrNoByContactNoAsync(contactNo);
        }

        public Task<bool> ContactBelongsToMrNoAsync(string contactNo, string mrNo)
        {
            return _repo.ContactBelongsToMrNoAsync(contactNo, mrNo);
        }
    }
}
