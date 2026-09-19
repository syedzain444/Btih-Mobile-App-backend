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

            if (request.PinHash.Trim().Length < 32)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "PIN hash is invalid",
                });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _appPinService.UpsertPinAsync(
                request.MrNo,
                request.PinHash,
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
        public string MrNo { get; set; } = string.Empty;
        public string PinHash { get; set; } = string.Empty;
        public string? DeviceLabel { get; set; }
    }

    public class ClearAppPinRequest
    {
        public string MrNo { get; set; } = string.Empty;
    }
}
