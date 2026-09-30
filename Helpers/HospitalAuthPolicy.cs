using System.Text.RegularExpressions;

namespace HospitalMobileAPPApi.Helpers
{
    /// <summary>
    /// Hospital patient-portal auth policy (REQ-2026-016 / TC-016).
    /// Login: validate phone format + non-empty password (legacy passwords allowed).
    /// Password create/update: full complexity rules.
    /// </summary>
    public static class HospitalAuthPolicy
    {
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 64;
        public const int MinPhoneDigits = 10;
        public const int MaxPhoneDigits = 15;

        private static readonly Regex HasUpper = new("[A-Z]", RegexOptions.Compiled);
        private static readonly Regex HasLower = new("[a-z]", RegexOptions.Compiled);
        private static readonly Regex HasDigit = new("[0-9]", RegexOptions.Compiled);
        private static readonly Regex HasSpecial = new(@"[^A-Za-z0-9]", RegexOptions.Compiled);
        private static readonly Regex PakistanMobile = new(@"^03\d{9}$", RegexOptions.Compiled);
        /// <summary>HMIS MR patterns e.g. 010-002-152 or 010002152.</summary>
        private static readonly Regex MrNumberPattern = new(
            @"^\d{2,4}-\d{2,5}-\d{2,6}$|^\d{7,12}$",
            RegexOptions.Compiled);

        public static string PasswordPolicySummary =>
            $"Password must be {MinPasswordLength}–{MaxPasswordLength} characters and include uppercase, lowercase, a number, and a special character.";

        public enum LoginIdentifierKind
        {
            Phone,
            MrNumber,
        }

        /// <summary>True when input looks like an HMIS MR number rather than a mobile.</summary>
        public static bool LooksLikeMrNumber(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var trimmed = raw.Trim();

            // Classic dashed MR (010-002-152) — never a phone.
            if (Regex.IsMatch(trimmed, @"^\d{2,4}-\d{2,5}-\d{2,6}$"))
            {
                return true;
            }

            // Digit-only MR candidates: not Pakistan mobile and not 10–15 digit phone-like.
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (PakistanMobile.IsMatch(NormalizePakistanPhone(trimmed)))
            {
                return false;
            }

            if (digits.Length is >= 7 and <= 12
                && !digits.StartsWith("03")
                && digits.Length < MinPhoneDigits)
            {
                return MrNumberPattern.IsMatch(trimmed) || Regex.IsMatch(trimmed, @"^\d{7,12}$");
            }

            // Explicit MR label prefix from some UI paste formats.
            if (trimmed.StartsWith("MR", StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmed[2..].TrimStart(' ', ':', '#', '-');
                return Regex.IsMatch(rest, @"^\d{2,4}-\d{2,5}-\d{2,6}$")
                       || Regex.IsMatch(rest, @"^\d{7,12}$");
            }

            return false;
        }

        public static string NormalizeMrNumber(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("MR", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[2..].TrimStart(' ', ':', '#', '-');
            }

            if (Regex.IsMatch(trimmed, @"^\d{2,4}-\d{2,5}-\d{2,6}$"))
            {
                return trimmed.ToUpperInvariant();
            }

            return trimmed.Trim();
        }

        /// <summary>
        /// Validates login identifier as MR number or registered mobile (REQ-2026-017).
        /// </summary>
        public static bool TryValidateLoginIdentifier(
            string? identifier,
            out LoginIdentifierKind kind,
            out string normalized,
            out string? error)
        {
            kind = LoginIdentifierKind.Phone;
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(identifier))
            {
                error = "MR number or mobile number is required";
                return false;
            }

            var trimmed = identifier.Trim();

            if (LooksLikeMrNumber(trimmed))
            {
                kind = LoginIdentifierKind.MrNumber;
                normalized = NormalizeMrNumber(trimmed);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    error = "Enter a valid MR number (e.g. 010-002-152)";
                    return false;
                }

                error = null;
                return true;
            }

            return TryValidateLoginContact(trimmed, out normalized, out error);
        }

        /// <summary>Normalize Pakistani mobile to <c>03XXXXXXXXX</c> when possible.</summary>
        public static string NormalizePakistanPhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("92") && digits.Length >= 12)
            {
                digits = "0" + digits[2..];
            }
            else if (!digits.StartsWith("0") && digits.Length == 10)
            {
                digits = "0" + digits;
            }

            return digits;
        }

        public static bool TryValidateLoginContact(string? contactNo, out string normalized, out string? error)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(contactNo))
            {
                error = "Contact number is required";
                return false;
            }

            var trimmed = contactNo.Trim();
            if (!Regex.IsMatch(trimmed, @"^[0-9+\-\s]+$"))
            {
                error = "Contact number can only contain digits, spaces, + or -";
                return false;
            }

            normalized = NormalizePakistanPhone(trimmed);
            if (PakistanMobile.IsMatch(normalized))
            {
                error = null;
                return true;
            }

            // Fallback for legacy HMIS contact formats (10–15 digits).
            if (normalized.Length is >= MinPhoneDigits and <= MaxPhoneDigits)
            {
                error = null;
                return true;
            }

            error = "Enter a valid mobile number (e.g. 03XXXXXXXXX)";
            return false;
        }

        public static bool TryValidateLoginPassword(string? password, out string? error)
        {
            if (string.IsNullOrEmpty(password))
            {
                error = "Password is required";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Full hospital password policy for create / reset / change.</summary>
        public static bool TryValidateNewPassword(
            string? password,
            out string? error,
            string? currentPassword = null,
            string? confirmPassword = null)
        {
            if (string.IsNullOrEmpty(password))
            {
                error = "Password is required";
                return false;
            }

            if (password.Length < MinPasswordLength)
            {
                error = $"Password must be at least {MinPasswordLength} characters";
                return false;
            }

            if (password.Length > MaxPasswordLength)
            {
                error = $"Password must be at most {MaxPasswordLength} characters";
                return false;
            }

            if (password.Any(char.IsWhiteSpace))
            {
                error = "Password cannot contain spaces";
                return false;
            }

            if (!HasUpper.IsMatch(password))
            {
                error = "Password must include at least one uppercase letter";
                return false;
            }

            if (!HasLower.IsMatch(password))
            {
                error = "Password must include at least one lowercase letter";
                return false;
            }

            if (!HasDigit.IsMatch(password))
            {
                error = "Password must include at least one number";
                return false;
            }

            if (!HasSpecial.IsMatch(password))
            {
                error = "Password must include at least one special character";
                return false;
            }

            if (confirmPassword != null && password != confirmPassword)
            {
                error = "Password and confirm password do not match";
                return false;
            }

            if (!string.IsNullOrEmpty(currentPassword) &&
                string.Equals(password, currentPassword, StringComparison.Ordinal))
            {
                error = "New password must be different from the current password";
                return false;
            }

            error = null;
            return true;
        }

        public static object ValidationFailure(string message, params string[] errors) => new
        {
            success = false,
            message,
            errors = errors.Length > 0 ? errors : new[] { message },
            policy = PasswordPolicySummary,
        };
    }
}
