using System.Text.RegularExpressions;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Security")]
    public class AppPinController : ControllerBase
    {
        /// <summary>SHA-256 hex digest length (what the Flutter app sends).</summary>
        private static readonly Regex Sha256Hex = new(
            @"^[a-fA-F0-9]{64}$",
            RegexOptions.Compiled);

        private readonly IAppPinService _appPinService;

        public AppPinController(IAppPinService appPinService)
        {
            _appPinService = appPinService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus([FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var hasPin = await _appPinService.HasActivePinAsync(mrNo);
            return Ok(new { success = true, hasPin });
        }

        [HttpPost("set")]
        public async Task<IActionResult> SetPin([FromBody] SetAppPinRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) ||
                string.IsNullOrWhiteSpace(request.PinHash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "MR number and PIN hash are required",
                });
            }

            var pinHash = request.PinHash.Trim();
            if (!Sha256Hex.IsMatch(pinHash))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "pinHash must be a 64-character SHA-256 hex string " +
                        "(hash of \"{mrNo}::{pin}\"). Do not send the JWT access token here.",
                });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _appPinService.UpsertPinAsync(
                request.MrNo,
                pinHash.ToLowerInvariant(),
                request.DeviceLabel);

            return Ok(new { success = true, message = "App PIN saved" });
        }

        [HttpPost("clear")]
        public async Task<IActionResult> ClearPin([FromBody] ClearAppPinRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _appPinService.ClearPinAsync(request.MrNo);
            return Ok(new { success = true, message = "App PIN cleared" });
        }
    }

    public class SetAppPinRequest
    {
        /// <example>010-002-152</example>
        public string MrNo { get; set; } = string.Empty;

        /// <summary>SHA-256 hex of "{mrNo}::{pin}" — not the JWT.</summary>
        /// <example>e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855</example>
        public string PinHash { get; set; } = string.Empty;

        /// <example>vivo V2061</example>
        public string? DeviceLabel { get; set; }
    }

    public class ClearAppPinRequest
    {
        public string MrNo { get; set; } = string.Empty;
    }
}
