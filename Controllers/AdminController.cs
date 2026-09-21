using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
    [Tags("Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IAuditLogService _auditLogService;

        public AdminController(IAdminService adminService, IAuditLogService auditLogService)
        {
            _adminService = adminService;
            _auditLogService = auditLogService;
        }

        /// <summary>Dashboard summary counts.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var tickets = await _adminService.GetOpenTicketsAsync();
            var refills = await _adminService.GetPendingRefillsAsync();
            var threads = await _adminService.GetMessageThreadsAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    openTickets = tickets.Count,
                    pendingRefills = refills.Count,
                    messageThreads = threads.Count,
                },
            });
        }

        /// <summary>All patient message threads for staff reply.</summary>
        [HttpGet("messages/threads")]
        public async Task<IActionResult> GetMessageThreads()
        {
            var threads = await _adminService.GetMessageThreadsAsync();
            return Ok(new { success = true, data = threads });
        }

        /// <summary>Full conversation history for a message thread.</summary>
        [HttpGet("messages/threads/{threadId:int}/messages")]
        public async Task<IActionResult> GetThreadMessages(
            int threadId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            var result = await _adminService.GetThreadMessagesAsync(threadId, pageNumber, pageSize);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Thread not found." });
            }

            return Ok(new
            {
                success = true,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize,
                totalRecords = result.TotalRecords,
                totalPages = result.TotalPages,
                data = result.Data,
            });
        }

        /// <summary>Staff reply to a patient message thread.</summary>
        [HttpPost("messages/reply")]
        public async Task<IActionResult> ReplyToThread([FromBody] AdminReplyMessageRequest request)
        {
            if (request.ThreadId <= 0 || string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new { success = false, message = "Thread ID and message body are required." });
            }

            var message = await _adminService.ReplyToThreadAsync(request);
            if (message == null)
            {
                return NotFound(new { success = false, message = "Thread not found." });
            }

            return Ok(new { success = true, data = message });
        }

        /// <summary>Paginated portal user list with optional search.</summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _adminService.GetPortalUsersAsync(search, page, pageSize);
            return Ok(new
            {
                success = true,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize,
                totalRecords = result.TotalRecords,
                totalPages = result.TotalPages,
                data = result.Data,
            });
        }

        /// <summary>Paginated appointment list with optional filters.</summary>
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] string? status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _adminService.GetAppointmentsAsync(status, from, to, search, page, pageSize);
            return Ok(new
            {
                success = true,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize,
                totalRecords = result.TotalRecords,
                totalPages = result.TotalPages,
                data = result.Data,
            });
        }

        /// <summary>Approve a pending appointment request.</summary>
        [HttpPost("appointments/{id}/approve")]
        public async Task<IActionResult> ApproveAppointment(
            string id,
            [FromBody] AdminAppointmentActionRequest? request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { success = false, message = "Appointment ID is required." });
            }

            var appointment = await _adminService.ApproveAppointmentAsync(id, request?.Notes);
            return appointment == null
                ? NotFound(new { success = false, message = "Appointment not found or cannot be approved." })
                : Ok(new { success = true, message = "Appointment approved.", data = appointment });
        }

        /// <summary>Reject a pending appointment request.</summary>
        [HttpPost("appointments/{id}/reject")]
        public async Task<IActionResult> RejectAppointment(
            string id,
            [FromBody] AdminAppointmentActionRequest? request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { success = false, message = "Appointment ID is required." });
            }

            var appointment = await _adminService.RejectAppointmentAsync(id, request?.Notes);
            return appointment == null
                ? NotFound(new { success = false, message = "Appointment not found or cannot be rejected." })
                : Ok(new { success = true, message = "Appointment rejected.", data = appointment });
        }

        /// <summary>Pending medication refill requests.</summary>
        [HttpGet("refills/pending")]
        public async Task<IActionResult> GetPendingRefills()
        {
            var refills = await _adminService.GetPendingRefillsAsync();
            return Ok(new { success = true, data = refills });
        }

        /// <summary>Update refill request status.</summary>
        [HttpPut("refills/status")]
        public async Task<IActionResult> UpdateRefill([FromBody] AdminUpdateRefillRequest request)
        {
            if (request.RefillId <= 0 || string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new { success = false, message = "Refill ID and status are required." });
            }

            var updated = await _adminService.UpdateRefillAsync(request);
            return updated
                ? Ok(new { success = true, message = "Refill status updated." })
                : NotFound(new { success = false, message = "Refill request not found." });
        }

        /// <summary>Open support tickets.</summary>
        [HttpGet("support/tickets")]
        public async Task<IActionResult> GetOpenTickets()
        {
            var tickets = await _adminService.GetOpenTicketsAsync();
            return Ok(new { success = true, data = tickets });
        }

        /// <summary>Update support ticket status and notes.</summary>
        [HttpPut("support/tickets")]
        public async Task<IActionResult> UpdateTicket([FromBody] AdminUpdateTicketRequest request)
        {
            if (request.TicketId <= 0 || string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new { success = false, message = "Ticket ID and status are required." });
            }

            var updated = await _adminService.UpdateTicketAsync(request);
            return updated
                ? Ok(new { success = true, message = "Ticket updated." })
                : NotFound(new { success = false, message = "Ticket not found." });
        }

        /// <summary>Recent audit log entries (admin only).</summary>
        [HttpGet("audit")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public async Task<IActionResult> GetAuditLog([FromQuery] int take = 100, [FromQuery] string? mrNo = null)
        {
            var logs = await _auditLogService.GetRecentAsync(take, mrNo);
            return Ok(new { success = true, data = logs });
        }
    }
}
