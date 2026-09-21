using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/admin/analytics")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
    [Tags("Admin")]
    public class AdminAnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AdminAnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>User statistics — total, active, new, and returning portal users.</summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var data = await _analyticsService.GetUserStatisticsAsync(from, to);
            return Ok(new { success = true, data });
        }

        /// <summary>Visit analytics — daily, weekly, and monthly app visits.</summary>
        [HttpGet("visits")]
        public async Task<IActionResult> GetVisits([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var data = await _analyticsService.GetVisitAnalyticsAsync(from, to);
            return Ok(new { success = true, data });
        }

        /// <summary>Engagement metrics — session counts, duration, and time spent.</summary>
        [HttpGet("engagement")]
        public async Task<IActionResult> GetEngagement([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var data = await _analyticsService.GetEngagementMetricsAsync(from, to);
            return Ok(new { success = true, data });
        }
    }
}
