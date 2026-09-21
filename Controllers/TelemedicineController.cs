using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
    [Tags("Telemedicine")]
    public class TelemedicineController : ControllerBase
    {
        private readonly ITelemedicineService _telemedicineService;

        public TelemedicineController(ITelemedicineService telemedicineService) =>
            _telemedicineService = telemedicineService;

        /// <summary>Schedule a telemedicine session.</summary>
        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody] CreateTelemedSessionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var session = await _telemedicineService.CreateSessionAsync(request);
            return Ok(new { success = true, data = session });
        }

        /// <summary>List telemedicine sessions for a patient.</summary>
        [HttpGet("sessions/{mrNo}")]
        public async Task<IActionResult> GetSessions(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var sessions = await _telemedicineService.GetSessionsAsync(mrNo.Trim());
            return Ok(new { success = true, data = sessions });
        }

        /// <summary>Get session details including join URL.</summary>
        [HttpGet("sessions/detail/{sessionId:int}")]
        public async Task<IActionResult> GetSession(int sessionId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var session = await _telemedicineService.GetSessionAsync(sessionId, mrNo.Trim());
            return session == null
                ? NotFound(new { success = false, message = "Session not found." })
                : Ok(new { success = true, data = session });
        }

        /// <summary>Update session status (start, complete, cancel).</summary>
        [HttpPatch("sessions/{sessionId:int}/status")]
        public async Task<IActionResult> UpdateStatus(int sessionId, [FromQuery] string mrNo, [FromQuery] string status)
        {
            if (string.IsNullOrWhiteSpace(mrNo) || string.IsNullOrWhiteSpace(status))
            {
                return BadRequest(new { success = false, message = "MR number and status are required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            try
            {
                var session = await _telemedicineService.UpdateStatusAsync(sessionId, status, mrNo.Trim());
                return session == null
                    ? NotFound(new { success = false, message = "Session not found." })
                    : Ok(new { success = true, data = session });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
