namespace HospitalMobileAPPApi.Models
{
    public class RegisterRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public bool AcceptTerms { get; set; }
        public string Otp { get; set; } = string.Empty;
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public DateTime? ExpiresAt { get; set; }
        public int ExpiresInSeconds { get; set; }
        public string? MrNo { get; set; }
        public string? FirstName { get; set; }
        public bool ProfileSetupRequired { get; set; }
        public bool IsExistingHmisPatient { get; set; }
    }

    public class ProfileSetupRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? CNIC { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? BloodGroup { get; set; }
        public string? Email { get; set; }
    }

    public class MobilePatientRecord
    {
        public string MrNo { get; set; } = string.Empty;
        public string ContactNo { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool ProfileSetupComplete { get; set; }
    }
}
