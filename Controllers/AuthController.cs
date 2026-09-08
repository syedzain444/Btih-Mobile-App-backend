using HospitalMobileAPPApi.Configuration;
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

        private readonly IAuthService _authService;
        private readonly IMemoryCache _cache;
        private readonly IJwtService _jwtService;
        private readonly IPatientService _patientService;
        private readonly ILogger<AuthController> _logger;
        private readonly AuthSettings _authSettings;
        private readonly SmsSettings _smsSettings;

        public AuthController(
            IAuthService authService,
            IMemoryCache cache,
            IJwtService jwtService,
            IPatientService patientService,
            ILogger<AuthController> logger,
            IOptions<AuthSettings> authSettings,
            IOptions<SmsSettings> smsSettings)
        {
            _authService = authService;
            _cache = cache;
            _jwtService = jwtService;
            _patientService = patientService;
            _logger = logger;
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

            return Ok(new
            {
                message = "Verification successful",
                mr_no = result.MR_NO,
                contactno = result.CONTACT_NO,
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

