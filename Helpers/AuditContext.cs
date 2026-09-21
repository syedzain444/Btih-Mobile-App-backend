using System.Security.Claims;

namespace HospitalMobileAPPApi.Helpers
{
    public static class AuditContext
    {
        public static string? GetActorId(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(ClaimTypes.Name);
        }

        public static string? GetActorRole(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Role);
        }
    }
}
