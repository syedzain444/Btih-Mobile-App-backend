using System.Text.Json;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Dashboard recent-activity feed persisted per patient MR.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("RecentActivity")]
    public class RecentActivityController : ControllerBase
    {
        private readonly IRecentActivityService _recentActivityService;

        public RecentActivityController(IRecentActivityService recentActivityService)
        {
            _recentActivityService = recentActivityService;
        }

        /// <summary>Get latest recent-activity rows for a patient (default 3).</summary>
        [HttpGet]
        public async Task<IActionResult> GetLatest(
            [FromQuery] string mrNo,
            [FromQuery] int limit = 3)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var rows = await _recentActivityService.GetLatestAsync(mrNo.Trim(), limit);
            return Ok(new
            {
                success = true,
                count = rows.Count,
                data = rows.Select(MapActivity),
            });
        }

        /// <summary>Upsert one activity and keep only the newest 3 for the patient.</summary>
        [HttpPost]
        public async Task<IActionResult> Record([FromBody] RecordRecentActivityRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            try
            {
                var saved = await _recentActivityService.RecordAsync(request);
                return Ok(new
                {
                    success = true,
                    message = "Activity recorded",
                    data = MapActivity(saved),
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>Clear all recent activity for a patient.</summary>
        [HttpDelete]
        public async Task<IActionResult> Clear([FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var deleted = await _recentActivityService.ClearAsync(mrNo.Trim());
            return Ok(new
            {
                success = true,
                deleted,
            });
        }

        private static object MapActivity(PatientRecentActivityRecord row)
        {
            object? payload = null;
            if (!string.IsNullOrWhiteSpace(row.PayloadJson))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<JsonElement>(row.PayloadJson);
                }
                catch
                {
                    payload = row.PayloadJson;
                }
            }

            return new
            {
                activityId = row.ActivityId,
                mrNo = row.MrNo,
                id = row.ActivityKey,
                activityKey = row.ActivityKey,
                kind = row.Kind,
                title = row.Title,
                subtitle = row.Subtitle,
                payload,
                viewedAt = row.ViewedAt,
            };
        }
    }
}
