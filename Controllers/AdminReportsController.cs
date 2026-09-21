using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/admin/reports")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
    [Tags("Admin")]
    public class AdminReportsController : ControllerBase
    {
        private readonly IAdminReportService _reportService;

        public AdminReportsController(IAdminReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>Registration report grouped by day.</summary>
        [HttpGet("registrations")]
        public async Task<IActionResult> GetRegistrations(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var data = await _reportService.GetRegistrationsReportAsync(from, to);
            return Ok(new { success = true, data });
        }

        /// <summary>Appointment report with status breakdown.</summary>
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] string? status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var data = await _reportService.GetAppointmentsReportAsync(status, from, to);
            return Ok(new { success = true, data });
        }

        /// <summary>Export engagement session data as CSV.</summary>
        [HttpGet("engagement/export")]
        public async Task<IActionResult> ExportEngagement(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var csv = await _reportService.ExportEngagementAsync(from, to);
            var fileName = $"engagement-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(csv, "text/csv", fileName);
        }
    }
}
