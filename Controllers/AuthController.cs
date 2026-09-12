using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Authentication, OTP verification, and password reset endpoints.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [Tags("Auth")]
    public class AuthController : ControllerBase    {
        private const string PasswordResetCachePrefix = "pwd_reset_verified:";
        private const string RegistrationOtpCachePrefix = "reg_otp:";
        private const string LoginChallengeCachePrefix = "login_challenge:";

        private readonly IAuthService _authService;
        private readonly IMemoryCache _cache;
        private readonly IJwtService _jwtService;
        private readonly IPatientService _patientService;
        private readonly IRegistrationService _registrationService;
        private readonly ITrustedDeviceService _trustedDeviceService;
        private readonly ILogger<AuthController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly AuthSettings _authSettings;
        private readonly SmsSettings _smsSettings;

        public AuthController(
            IAuthService authService,
            IMemoryCache cache,
            IJwtService jwtService,
            IPatientService patientService,
            IRegistrationService registrationService,
            ITrustedDeviceService trustedDeviceService,
            ILogger<AuthController> logger,
            IWebHostEnvironment environment,
            IOptions<AuthSettings> authSettings,
            IOptions<SmsSettings> smsSettings)
        {
            _authService = authService;
            _cache = cache;
            _jwtService = jwtService;
            _patientService = patientService;
            _registrationService = registrationService;
            _trustedDeviceService = trustedDeviceService;
            _logger = logger;
            _environment = environment;
            _authSettings = authSettings.Value;
            _smsSettings = smsSettings.Value;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ContactNo) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Contact number and password are required" });
            }

            try
            {
                var loginResult = await _authService.LoginAsync(
                    request.ContactNo.Trim(),
                    request.Password);

                if (loginResult == null || string.IsNullOrWhiteSpace(loginResult.MrNo))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid contact number or password",
                    });
                }

                var contactNo = request.ContactNo.Trim();
                var deviceInstallId = request.DeviceInstallId?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(deviceInstallId))
                {
                    return Ok(BuildLoginSuccessResponse(loginResult.MrNo, contactNo, loginResult.FirstName));
                }

                if (ShouldDevAutoTrust(contactNo))
                {
                    var devLogin = await TryDevAutoTrustLoginAsync(
                        loginResult.MrNo,
                        contactNo,
                        loginResult.FirstName,
                        deviceInstallId,
                        request.DeviceLabel,
                        request.Platform);

                    if (devLogin != null)
                    {
                        return Ok(devLogin);
                    }

                    _logger.LogWarning(
                        "Development bypass login for {ContactNo} (trusted-device store unavailable)",
                        contactNo);
                    return Ok(BuildLoginSuccessResponse(
                        loginResult.MrNo,
                        contactNo,
                        loginResult.FirstName));
                }

                var isTrusted = false;
                try
                {
                    isTrusted = await _trustedDeviceService.IsTrustedDeviceAsync(
                        loginResult.MrNo,
                        deviceInstallId,
                        request.DeviceTrustToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Trusted device check failed for MR {MrNo}; requiring login OTP",
                        loginResult.MrNo);
                }

                if (isTrusted)
                {
                    return Ok(BuildLoginSuccessResponse(loginResult.MrNo, contactNo, loginResult.FirstName));
                }

                var challenge = await BeginLoginOtpChallengeAsync(
                    loginResult.MrNo,
                    contactNo,
                    loginResult.FirstName,
                    deviceInstallId,
                    request.DeviceLabel,
                    request.Platform);

                if (challenge == null)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Could not send login verification code. Please try again.",
                    });
                }

                return Ok(challenge);
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                _logger.LogError(ex, "Database connection failed during login");
                return StatusCode(statusCode, new
                {
                    success = false,
                    message = dbMessage,
                    hint = "This is an Oracle database connection error, not invalid patient credentials.",
                });
            }
        }

        [HttpPost("verify-login-otp")]
        public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyLoginOtpRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.LoginChallengeId) ||
                string.IsNullOrWhiteSpace(request.Otp) ||
                string.IsNullOrWhiteSpace(request.DeviceInstallId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Login challenge id, OTP, and device id are required",
                });
            }

            var cacheKey = $"{LoginChallengeCachePrefix}{request.LoginChallengeId.Trim()}";
            if (!_cache.TryGetValue(cacheKey, out LoginChallengeCacheEntry? challenge) || challenge == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid or expired login verification session",
                });
            }

            if (!string.Equals(challenge.Otp, request.Otp.Trim(), StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid or expired OTP",
                });
            }

            _cache.Remove(cacheKey);

            string? deviceTrustToken = null;
            if (request.TrustDevice)
            {
                try
                {
                    deviceTrustToken = await _trustedDeviceService.RegisterTrustedDeviceAsync(
                        challenge.MrNo,
                        request.DeviceInstallId.Trim(),
                        request.DeviceLabel ?? challenge.DeviceLabel,
                        request.Platform ?? challenge.Platform);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register trusted device for MR {MrNo}", challenge.MrNo);
                }
            }

            var tokenResult = _jwtService.GenerateToken(challenge.MrNo, challenge.ContactNo);
            return Ok(new
            {
                success = true,
                message = "Login successful",
                requiresOtp = false,
                token = tokenResult.Token,
                tokenType = "Bearer",
                expiresAt = tokenResult.ExpiresAt,
                expiresInSeconds = tokenResult.ExpiresInSeconds,
                mrNo = challenge.MrNo,
                firstName = challenge.FirstName,
                deviceTrustToken,
            });
        }

        [HttpPost("verifyPhoneNo")]
        public async Task<IActionResult> VerifyNumber(string? ContactNo, string? mrno)
        {
            if (string.IsNullOrWhiteSpace(ContactNo) && string.IsNullOrWhiteSpace(mrno))
            {
                return BadRequest(new { message = "Contact number or MR number is required" });
            }

            var result = await _authService.VerifyPhoneNo(ContactNo?.Trim(), mrno?.Trim());

            if (result.MR_NO == null && result.CONTACT_NO == null)
            {
                return Unauthorized(new { message = "Invalid number" });
            }

            var contact = result.CONTACT_NO ?? ContactNo?.Trim() ?? string.Empty;
            var hasPortalAccount = await _registrationService.HasPortalAccountAsync(
                contact,
                result.MR_NO ?? string.Empty);

            return Ok(new
            {
                message = "Verification successful",
                mr_no = result.MR_NO,
                mrNo = result.MR_NO,
                contactno = result.CONTACT_NO,
                contactNo = result.CONTACT_NO,
                hasPortalAccount,
            });
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return BadRequest(new { success = false, message = "Phone number is required" });
            }

            phoneNumber = phoneNumber.Trim();

            try
            {
                var mrNo = await _authService.GetMrNoByContactNoAsync(phoneNumber);
                if (string.IsNullOrWhiteSpace(mrNo))
                {
                    return NotFound(new { success = false, message = "Phone number is not registered" });
                }

                var otp = GenerateOtp();
                var expiry = TimeSpan.FromMinutes(_authSettings.OtpExpiryMinutes);

                _cache.Set(phoneNumber, otp, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry,
                });

                var message = $"Your OTP is {otp}. It will expire in {_authSettings.OtpExpiryMinutes} minutes.";
                var smsSent = await SendSmsAsync(phoneNumber, message);

                if (!smsSent)
                {
                    if (_environment.IsDevelopment() || _smsSettings.ReturnDebugOtpOnFailure)
                    {
                        _logger.LogWarning(
                            "SMS delivery failed for {PhoneNumber}. Returning debug OTP because fallback is enabled.",
                            phoneNumber);

                        return Ok(new
                        {
                            success = true,
                            message = "OTP generated. SMS could not be delivered — use the code shown below.",
                            expiresInMinutes = _authSettings.OtpExpiryMinutes,
                            smsDelivered = false,
                            debugOtp = otp,
                        });
                    }

                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Failed to send OTP. SMS service is unavailable.",
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "OTP sent successfully",
                    expiresInMinutes = _authSettings.OtpExpiryMinutes,
                    smsDelivered = true,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP to {PhoneNumber}", phoneNumber);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error sending OTP",
                });
            }
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp(string phoneNumber, string otp)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(otp))
            {
                return BadRequest(new { success = false, message = "Phone number and OTP are required" });
            }

            phoneNumber = phoneNumber.Trim();
            otp = otp.Trim();

            if (!_cache.TryGetValue(phoneNumber, out string? storedOtp) || storedOtp != otp)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid or expired OTP",
                });
            }

            _cache.Remove(phoneNumber);

            var mrNo = await _authService.GetMrNoByContactNoAsync(phoneNumber);
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return NotFound(new { success = false, message = "Phone number is not registered" });
            }

            _cache.Set(
                $"{PasswordResetCachePrefix}{mrNo}",
                phoneNumber,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_authSettings.PasswordResetWindowMinutes),
                });

            return Ok(new
            {
                success = true,
                message = "OTP verified successfully",
                mrNo,
            });
        }

        [HttpPost("send-registration-otp")]
        public async Task<IActionResult> SendRegistrationOtp(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return BadRequest(new { success = false, message = "Phone number is required" });
            }

            phoneNumber = phoneNumber.Trim();

            try
            {
                var otp = GenerateOtp();
                var expiry = TimeSpan.FromMinutes(_authSettings.OtpExpiryMinutes);
                var cacheKey = $"{RegistrationOtpCachePrefix}{phoneNumber}";

                _cache.Set(cacheKey, otp, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry,
                });

                var message = $"Your BTIH registration OTP is {otp}. It will expire in {_authSettings.OtpExpiryMinutes} minutes.";
                var smsSent = await SendSmsAsync(phoneNumber, message);

                if (!smsSent)
                {
                    if (_environment.IsDevelopment() || _smsSettings.ReturnDebugOtpOnFailure)
                    {
                        _logger.LogWarning(
                            "Registration SMS delivery failed for {PhoneNumber}. Returning debug OTP.",
                            phoneNumber);

                        return Ok(new
                        {
                            success = true,
                            message = "Registration OTP generated. SMS could not be delivered — use the code shown below.",
                            expiresInMinutes = _authSettings.OtpExpiryMinutes,
                            smsDelivered = false,
                            debugOtp = otp,
                        });
                    }

                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Failed to send OTP. SMS service is unavailable.",
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Registration OTP sent successfully",
                    expiresInMinutes = _authSettings.OtpExpiryMinutes,
                    smsDelivered = true,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send registration OTP to {PhoneNumber}", phoneNumber);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error sending registration OTP",
                });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Otp))
            {
                return BadRequest(new { success = false, message = "Phone number and OTP are required" });
            }

            var phoneNumber = request.PhoneNumber.Trim();
            var cacheKey = $"{RegistrationOtpCachePrefix}{phoneNumber}";

            if (!_cache.TryGetValue(cacheKey, out string? storedOtp) || storedOtp != request.Otp.Trim())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid or expired OTP. Call send-registration-otp first.",
                });
            }

            _cache.Remove(cacheKey);

            var result = await _registrationService.RegisterAsync(request);

            if (!result.Success || string.IsNullOrWhiteSpace(result.MrNo))
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                });
            }

            var tokenResult = _jwtService.GenerateToken(result.MrNo, phoneNumber);

            return Ok(new
            {
                success = true,
                message = result.Message,
                token = tokenResult.Token,
                tokenType = result.TokenType,
                expiresAt = tokenResult.ExpiresAt,
                expiresInSeconds = tokenResult.ExpiresInSeconds,
                mrNo = result.MrNo,
                firstName = result.FirstName,
                profileSetupRequired = result.ProfileSetupRequired,
                isExistingHmisPatient = result.IsExistingHmisPatient,
            });
        }

        [HttpPost("updatePassword")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || string.IsNullOrWhiteSpace(request.PatientPassword))
            {
                return BadRequest(new { message = "MR number and new password are required" });
            }

            if (request.PatientPassword.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters" });
            }

            var cacheKey = $"{PasswordResetCachePrefix}{request.MrNo.Trim()}";
            if (!_cache.TryGetValue(cacheKey, out _))
            {
                return Unauthorized(new
                {
                    message = "Password reset is not authorized. Verify OTP first.",
                });
            }

            var updated = await _patientService.UpdatePatientPassword(request.MrNo.Trim(), request.PatientPassword);
            if (!updated)
            {
                return BadRequest(new { message = "Password update failed" });
            }

            _cache.Remove(cacheKey);

            try
            {
                await _trustedDeviceService.RevokeAllTrustedDevicesAsync(request.MrNo.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke trusted devices after password reset for MR {MrNo}", request.MrNo);
            }

            return Ok(new { message = "Password updated successfully" });
        }

        private object BuildLoginSuccessResponse(
            string mrNo,
            string contactNo,
            string? firstName,
            string? deviceTrustToken = null)
        {
            var tokenResult = _jwtService.GenerateToken(mrNo, contactNo);
            return new
            {
                success = true,
                message = "Login successful",
                requiresOtp = false,
                token = tokenResult.Token,
                tokenType = "Bearer",
                expiresAt = tokenResult.ExpiresAt,
                expiresInSeconds = tokenResult.ExpiresInSeconds,
                mrNo,
                firstName,
                deviceTrustToken,
            };
        }

        private bool ShouldDevAutoTrust(string contactNo)
        {
            if (_authSettings.DevAutoTrustContacts.Length == 0)
            {
                return false;
            }

            var bypassEnabled = _environment.IsDevelopment() || _smsSettings.ReturnDebugOtpOnFailure;
            if (!bypassEnabled)
            {
                return false;
            }

            var normalizedInput = NormalizeContactKey(contactNo);
            return _authSettings.DevAutoTrustContacts.Any(entry =>
                NormalizeContactKey(entry) == normalizedInput);
        }

        private async Task<object?> TryDevAutoTrustLoginAsync(
            string mrNo,
            string contactNo,
            string? firstName,
            string deviceInstallId,
            string? deviceLabel,
            string? platform)
        {
            try
            {
                var deviceTrustToken = await _trustedDeviceService.RegisterTrustedDeviceAsync(
                    mrNo,
                    deviceInstallId,
                    deviceLabel,
                    platform);

                _logger.LogWarning(
                    "Development auto-trust applied for contact {ContactNo} on device {DeviceInstallId}",
                    contactNo,
                    deviceInstallId);

                return BuildLoginSuccessResponse(
                    mrNo,
                    contactNo,
                    firstName,
                    deviceTrustToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Development auto-trust failed for MR {MrNo}; falling back to login OTP",
                    mrNo);
                return null;
            }
        }

        private static string NormalizeContactKey(string contactNo)
        {
            var digits = new string(contactNo.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length >= 12)
            {
                digits = digits[2..];
            }

            if (digits.StartsWith('0') && digits.Length > 1)
            {
                digits = digits[1..];
            }

            return digits;
        }

        private async Task<object?> BeginLoginOtpChallengeAsync(
            string mrNo,
            string contactNo,
            string? firstName,
            string deviceInstallId,
            string? deviceLabel,
            string? platform)
        {
            var otp = GenerateOtp();
            var challengeId = Guid.NewGuid().ToString("N");
            var cacheKey = $"{LoginChallengeCachePrefix}{challengeId}";
            var challenge = new LoginChallengeCacheEntry
            {
                MrNo = mrNo,
                ContactNo = contactNo,
                FirstName = firstName ?? string.Empty,
                DeviceInstallId = deviceInstallId,
                DeviceLabel = deviceLabel,
                Platform = platform,
                Otp = otp,
            };

            _cache.Set(cacheKey, challenge, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_authSettings.LoginChallengeMinutes),
            });

            var message =
                $"Your BTIH login verification code is {otp}. It expires in {_authSettings.OtpExpiryMinutes} minutes.";
            var smsTimeoutSeconds = _smsSettings.ReturnDebugOtpOnFailure
                ? Math.Min(_smsSettings.TimeoutSeconds, 8)
                : _smsSettings.TimeoutSeconds;
            var smsSent = await SendSmsAsync(contactNo, message, smsTimeoutSeconds);

            if (!smsSent)
            {
                if (_environment.IsDevelopment() || _smsSettings.ReturnDebugOtpOnFailure)
                {
                    return new
                    {
                        success = true,
                        requiresOtp = true,
                        loginChallengeId = challengeId,
                        message = "Verification code generated. SMS could not be delivered — use the code shown below.",
                        expiresInMinutes = _authSettings.OtpExpiryMinutes,
                        smsDelivered = false,
                        debugOtp = otp,
                        mrNo,
                        maskedContactNo = MaskContactNo(contactNo),
                    };
                }

                _cache.Remove(cacheKey);
                return null;
            }

            return new
            {
                success = true,
                requiresOtp = true,
                loginChallengeId = challengeId,
                message = "Verification code sent to your registered mobile number.",
                expiresInMinutes = _authSettings.OtpExpiryMinutes,
                smsDelivered = true,
                mrNo,
                maskedContactNo = MaskContactNo(contactNo),
            };
        }

        private static string MaskContactNo(string contactNo)
        {
            if (contactNo.Length <= 4)
            {
                return contactNo;
            }

            return $"{new string('*', contactNo.Length - 4)}{contactNo[^4..]}";
        }

        private static string GenerateOtp()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private Task<bool> SendSmsAsync(string number, string message)
        {
            return SendSmsAsync(number, message, _smsSettings.TimeoutSeconds);
        }

        private async Task<bool> SendSmsAsync(string number, string message, int timeoutSeconds)
        {
            try
            {
                var apiUrl = _smsSettings.BaseUrl + number + "&_Msg=" + WebUtility.UrlEncode(message);

                var request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Timeout = Math.Max(1, timeoutSeconds) * 1000;

                using var response = await request.GetResponseAsync() as HttpWebResponse;
                if (response == null || response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                await using var stream = response.GetResponseStream();
                if (stream == null)
                {
                    return false;
                }

                using var reader = new StreamReader(stream);
                await reader.ReadToEndAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS sending failed for {PhoneNumber}", number);
                return false;
            }
        }
    }
}

