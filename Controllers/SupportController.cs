using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Support")]
    public class SupportController : ControllerBase
    {
        private readonly ISupportService _supportService;

        public SupportController(ISupportService supportService) => _supportService = supportService;

        /// <summary>Hospital contact details for Help &amp; Support screen.</summary>
        [HttpGet("contact")]
        [AllowAnonymous]
        public async Task<IActionResult> GetContact()
        {
            var contact = await _supportService.GetContactInfoAsync();
            return Ok(new { success = true, data = contact });
        }

        /// <summary>FAQ list (English or Urdu).</summary>
        [HttpGet("faq")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFaq([FromQuery] string lang = "en", [FromQuery] string? category = null)
        {
            var faq = await _supportService.GetFaqAsync(lang, category);
            return Ok(new { success = true, lang, data = faq });
        }

        /// <summary>Submit a support ticket / complaint / suggestion (guest or authenticated patient).</summary>
        [HttpPost("tickets")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.ContactName) ||
                string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { success = false, message = "Contact name and message are required." });
            }

            if (request.Description.Trim().Length < 10)
            {
                return BadRequest(new { success = false, message = "Please provide a more detailed message (at least 10 characters)." });
            }

            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                request.Subject = string.IsNullOrWhiteSpace(request.Category)
                    ? "Support request"
                    : request.Category.Trim();
            }

            if (string.IsNullOrWhiteSpace(request.Category))
            {
                request.Category = "General";
            }

            if (!string.IsNullOrWhiteSpace(request.MrNo) &&
                User.Identity?.IsAuthenticated == true &&
                !PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var ticketId = await _supportService.CreateTicketAsync(request);
            return Ok(new
            {
                success = true,
                message = "Complaint / suggestion submitted. Staff have been notified.",
                ticketId,
            });
        }

        /// <summary>Patient's support tickets.</summary>
        [HttpGet("tickets/{mrNo}")]
        [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
        public async Task<IActionResult> GetTickets(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var tickets = await _supportService.GetTicketsAsync(mrNo.Trim());
            return Ok(new { success = true, data = tickets });
        }

        /// <summary>Single ticket detail.</summary>
        [HttpGet("tickets/detail/{ticketId:int}")]
        [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
        public async Task<IActionResult> GetTicket(int ticketId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var ticket = await _supportService.GetTicketAsync(ticketId, mrNo.Trim());
            return ticket == null
                ? NotFound(new { success = false, message = "Ticket not found." })
                : Ok(new { success = true, data = ticket });
        }
    }
}
