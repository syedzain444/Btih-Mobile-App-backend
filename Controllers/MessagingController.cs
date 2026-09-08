using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Messaging")]
    public class MessagingController : ControllerBase
    {
        private readonly IMessagingService _messagingService;
        private readonly MessagingSettings _settings;
        private readonly IWebHostEnvironment _environment;

        public MessagingController(
            IMessagingService messagingService,
            IOptions<MessagingSettings> settings,
            IWebHostEnvironment environment)
        {
            _messagingService = messagingService;
            _settings = settings.Value;
            _environment = environment;
        }

        [HttpPost("threads")]
        public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || string.IsNullOrWhiteSpace(request.Subject))
            {
                return BadRequest(new { success = false, message = "MR number and subject are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var threadId = await _messagingService.CreateThreadAsync(request);

            return Ok(new
            {
                success = true,
                message = "Thread created successfully",
                threadId,
            });
        }

        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox([FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var inbox = await _messagingService.GetInboxAsync(mrNo.Trim());
            return Ok(new { success = true, data = inbox });
        }

        [HttpGet("threads/{threadId:int}/messages")]
        public async Task<IActionResult> GetMessages(
            int threadId,
            [FromQuery] string mrNo,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var result = await _messagingService.GetMessagesAsync(threadId, mrNo.Trim(), pageNumber, pageSize);

            if (result.TotalRecords == 0 && result.Data.Count == 0)
            {
                var inbox = await _messagingService.GetInboxAsync(mrNo.Trim());
                if (!inbox.Any(t => t.ThreadId == threadId))
                {
                    return NotFound(new { success = false, message = "Thread not found" });
                }
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

        [HttpPost("threads/{threadId:int}/messages")]
        public async Task<IActionResult> SendMessage(int threadId, [FromBody] SendMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new { success = false, message = "MR number and message body are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            try
            {
                var message = await _messagingService.SendMessageAsync(threadId, request);
                return Ok(new { success = true, message = "Message sent", data = message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("threads/{threadId:int}/attachments")]
        [RequestSizeLimit(10485760)]
        public async Task<IActionResult> UploadAttachment(
            int threadId,
            [FromForm] string mrNo,
            [FromForm] string? body,
            IFormFile file)
        {
            if (string.IsNullOrWhiteSpace(mrNo) || file == null)
            {
                return BadRequest(new { success = false, message = "MR number and file are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var allowed = _settings.AllowedExtensions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : $".{e.ToLowerInvariant()}");

            try
            {
                var webRoot = _environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var message = await _messagingService.SendAttachmentAsync(
                    threadId,
                    mrNo.Trim(),
                    body,
                    file,
                    webRoot,
                    _settings.UploadSubPath,
                    _settings.MaxAttachmentBytes,
                    allowed);

                return Ok(new { success = true, message = "Attachment uploaded", data = message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
