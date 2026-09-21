using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
    [Tags("Analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>Record app session start (call when patient opens the app).</summary>
        [HttpPost("session/start")]
        public async Task<IActionResult> StartSession([FromBody] StartAppSessionRequest request)
        {
            var mrNo = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return Unauthorized(new { success = false, message = "Invalid patient token." });
            }

            var result = await _analyticsService.StartSessionAsync(mrNo, request);
            return Ok(new { success = true, data = result });
        }

        /// <summary>Record app session end (call when patient closes or backgrounds the app).</summary>
        [HttpPost("session/end")]
        public async Task<IActionResult> EndSession([FromBody] EndAppSessionRequest request)
        {
            var mrNo = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return Unauthorized(new { success = false, message = "Invalid patient token." });
            }

            if (string.IsNullOrWhiteSpace(request.SessionGuid))
            {
                return BadRequest(new { success = false, message = "Session GUID is required." });
            }

            var ended = await _analyticsService.EndSessionAsync(mrNo, request);
            return ended
                ? Ok(new { success = true, message = "Session recorded." })
                : NotFound(new { success = false, message = "Active session not found." });
        }
    }
}
