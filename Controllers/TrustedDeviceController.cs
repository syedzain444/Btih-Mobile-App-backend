using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Auth")]
    public class TrustedDeviceController : ControllerBase
    {
        private readonly ITrustedDeviceService _trustedDeviceService;

        public TrustedDeviceController(ITrustedDeviceService trustedDeviceService)
        {
            _trustedDeviceService = trustedDeviceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTrustedDevices([FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var devices = await _trustedDeviceService.GetTrustedDevicesAsync(mrNo);
            return Ok(new
            {
                success = true,
                data = devices.Select(device => new
                {
                    trustedDeviceId = device.TrustedDeviceId,
                    deviceInstallId = device.DeviceInstallId,
                    deviceLabel = device.DeviceLabel,
                    platform = device.Platform,
                    trustedAt = device.TrustedAt,
                    lastLoginAt = device.LastLoginAt,
                    expiresAt = device.ExpiresAt,
                }),
            });
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeTrustedDevice(
            [FromBody] RevokeTrustedDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || request.TrustedDeviceId <= 0)
            {
                return BadRequest(new { success = false, message = "MR number and device id are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var revoked = await _trustedDeviceService.RevokeTrustedDeviceAsync(
                request.MrNo,
                request.TrustedDeviceId);

            if (!revoked)
            {
                return NotFound(new { success = false, message = "Trusted device not found" });
            }

            return Ok(new { success = true, message = "Trusted device revoked" });
        }

        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAllTrustedDevices(
            [FromBody] RevokeAllTrustedDevicesRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _trustedDeviceService.RevokeAllTrustedDevicesAsync(request.MrNo);
            return Ok(new { success = true, message = "All trusted devices revoked" });
        }
    }
}
