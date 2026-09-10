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

        private readonly IAuthService _authService;
        private readonly IMemoryCache _cache;
        private readonly IJwtService _jwtService;
        private readonly IPatientService _patientService;
        private readonly IRegistrationService _registrationService;
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

                var tokenResult = _jwtService.GenerateToken(
                    loginResult.MrNo,
                    request.ContactNo.Trim());

                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    token = tokenResult.Token,
                    tokenType = "Bearer",
                    expiresAt = tokenResult.ExpiresAt,
                    expiresInSeconds = tokenResult.ExpiresInSeconds,
                    mrNo = loginResult.MrNo,
                    firstName = loginResult.FirstName,
                });
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

            return Ok(new { message = "Password updated successfully" });
        }

        private static string GenerateOtp()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private async Task<bool> SendSmsAsync(string number, string message)
        {
            try
            {
                var apiUrl = _smsSettings.BaseUrl + number + "&_Msg=" + WebUtility.UrlEncode(message);

                var request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Timeout = _smsSettings.TimeoutSeconds * 1000;

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

