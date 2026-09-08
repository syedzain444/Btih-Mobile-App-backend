using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IJwtService
    {
        JwtTokenResult GenerateToken(
            string userId,
            string username,
            string? role = null);
    }
}
