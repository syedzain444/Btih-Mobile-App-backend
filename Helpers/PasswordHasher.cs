using System.Security.Cryptography;
using System.Text;

namespace HospitalMobileAPPApi.Helpers
{
    public static class PasswordHasher
    {
        public static string Hash(string username, string password, string salt)
        {
            var payload = $"{username}:{password}:{salt}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes);
        }

        public static bool Verify(string username, string password, string salt, string expectedHash)
        {
            return string.Equals(Hash(username, password, salt), expectedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
