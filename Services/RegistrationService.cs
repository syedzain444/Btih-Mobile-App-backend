using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IAuthRepository _authRepository;
        private readonly IPatientRepository _patientRepository;

        public RegistrationService(
            IRegistrationRepository registrationRepository,
            IAuthRepository authRepository,
            IPatientRepository patientRepository)
        {
            _registrationRepository = registrationRepository;
            _authRepository = authRepository;
            _patientRepository = patientRepository;
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            if (!request.AcceptTerms)
            {
                return Fail("You must accept the terms and conditions");
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumber)
                || string.IsNullOrWhiteSpace(request.FirstName)
                || string.IsNullOrWhiteSpace(request.Password)
                || string.IsNullOrWhiteSpace(request.ConfirmPassword)
                || string.IsNullOrWhiteSpace(request.Otp))
            {
                return Fail("Phone number, first name, password, confirm password, and OTP are required");
            }

            if (request.Password != request.ConfirmPassword)
            {
                return Fail("Password and confirm password do not match");
            }

            if (request.Password.Length < 6)
            {
                return Fail("Password must be at least 6 characters");
            }

            var phone = request.PhoneNumber.Trim();

            if (await _registrationRepository.IsMobilePhoneRegisteredAsync(phone))
            {
                return Fail("This phone number is already registered. Please login.");
            }

            var hmisMrNo = await _authRepository.GetMrNoByContactNoAsync(phone);
            var isExistingHmis = !string.IsNullOrWhiteSpace(hmisMrNo);

            if (isExistingHmis)
            {
                if (await _registrationRepository.HasPortalPasswordAsync(hmisMrNo!))
                {
                    return Fail("This phone number is already registered. Please login.");
                }
            }

            string mrNo;
            string? firstName = request.FirstName.Trim();

            if (isExistingHmis)
            {
                mrNo = hmisMrNo!;
                await _patientRepository.UpdatePatientPassword(mrNo, request.Password);
                await _registrationRepository.UpdateHmisPatientNamesAsync(
                    mrNo,
                    firstName,
                    request.LastName?.Trim());
            }
            else
            {
                mrNo = await _registrationRepository.CreateMobilePatientAsync(
                    phone,
                    firstName,
                    request.LastName?.Trim(),
                    request.Password,
                    request.AcceptTerms);
            }

            var profileSetupRequired = await _registrationRepository.IsProfileSetupRequiredAsync(mrNo);

            return new RegisterResponse
            {
                Success = true,
                Message = "Registration successful",
                MrNo = mrNo,
                FirstName = firstName,
                ProfileSetupRequired = profileSetupRequired,
                IsExistingHmisPatient = isExistingHmis,
            };
        }

        public async Task<bool> CompleteProfileSetupAsync(ProfileSetupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return false;
            }

            if (request.MrNo.StartsWith("MOB-", StringComparison.OrdinalIgnoreCase))
            {
                return await _registrationRepository.CompleteMobileProfileSetupAsync(request);
            }

            var updated = await _patientRepository.UpdatePatientProfileAsync(new UpdatePatientProfileRequest
            {
                MrNo = request.MrNo,
                FirstName = request.FirstName,
                LastName = request.LastName,
                CNIC = request.CNIC,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                BloodGroup = request.BloodGroup,
                EmailAddress = request.Email,
            });

            return updated;
        }

        public async Task<PatientDetails?> GetPatientDetailsAsync(string mrNo)
        {
            if (mrNo.StartsWith("MOB-", StringComparison.OrdinalIgnoreCase))
            {
                return await _registrationRepository.GetMobilePatientDetailsAsync(mrNo);
            }

            var profile = await _patientRepository.GetPatientProfileAsync(mrNo, 1, 1);
            return profile?.Profile;
        }

        private static RegisterResponse Fail(string message)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = message,
            };
        }
    }
}
