using System.Security.Claims;

namespace HospitalMobileAPPApi.Helpers
{
    public static class PatientAuthorizationHelper
    {
        public static string? GetMrNoFromClaims(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.FindFirst("sub")?.Value;
        }

        public static bool IsAuthorizedForMrNo(ClaimsPrincipal user, string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return false;
            }

            var tokenMrNo = GetMrNoFromClaims(user);
            return !string.IsNullOrWhiteSpace(tokenMrNo)
                && string.Equals(tokenMrNo.Trim(), mrNo.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
