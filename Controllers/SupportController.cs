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
        public IActionResult GetContact()
        {
            return Ok(new { success = true, data = _supportService.GetContactInfo() });
        }

        /// <summary>FAQ list (English or Urdu).</summary>
        [HttpGet("faq")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFaq([FromQuery] string lang = "en", [FromQuery] string? category = null)
        {
            var faq = await _supportService.GetFaqAsync(lang, category);
            return Ok(new { success = true, lang, data = faq });
        }

        /// <summary>Submit a support ticket (guest or authenticated patient).</summary>
        [HttpPost("tickets")]
        public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ContactName) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { success = false, message = "Contact name, subject, and description are required." });
            }

            if (!string.IsNullOrWhiteSpace(request.MrNo) &&
                User.Identity?.IsAuthenticated == true &&
                !PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var ticketId = await _supportService.CreateTicketAsync(request);
            return Ok(new { success = true, message = "Support ticket submitted.", ticketId });
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
