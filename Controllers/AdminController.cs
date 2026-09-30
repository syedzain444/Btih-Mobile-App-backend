using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = AuthorizationPolicies.PortalAccess)]
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
            var isOps = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Staff);
            var threads = await _adminService.GetMessageThreadsAsync();
            var tickets = isOps
                ? await _adminService.GetOpenTicketsAsync()
                : [];
            var refills = isOps
                ? await _adminService.GetPendingRefillsAsync()
                : [];

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

        /// <summary>Paginated portal user list with optional search (Admin only).</summary>
        [HttpGet("users")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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

        /// <summary>Pending medication refill requests (Admin / Staff).</summary>
        [HttpGet("refills/pending")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> GetPendingRefills()
        {
            var refills = await _adminService.GetPendingRefillsAsync();
            return Ok(new { success = true, data = refills });
        }

        /// <summary>Update refill request status (Admin / Staff).</summary>
        [HttpPut("refills/status")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
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

        /// <summary>Open support tickets (Admin / Staff).</summary>
        [HttpGet("support/tickets")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> GetOpenTickets()
        {
            var tickets = await _adminService.GetOpenTicketsAsync();
            return Ok(new { success = true, data = tickets });
        }

        /// <summary>Update support ticket status and notes (Admin / Staff).</summary>
        [HttpPut("support/tickets")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
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

        /// <summary>List all launch promotions (Admin / Staff) plus app display limit.</summary>
        [HttpGet("promotions")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> GetPromotions(
            [FromServices] IPromotionService promotionService,
            [FromServices] IMobilePortalSchemaService schemaService)
        {
            try
            {
                await schemaService.EnsurePromotionSchemaAsync();
                var items = await promotionService.GetAllAsync();
                var displayLimit = await promotionService.GetDisplayLimitAsync();
                return Ok(new
                {
                    success = true,
                    displayLimit,
                    data = items.Select(MapPromotionAdmin),
                });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        /// <summary>How many active promotions the mobile app should show (1–50).</summary>
        [HttpPut("promotions/display-limit")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> SetPromotionDisplayLimit(
            [FromBody] PromotionDisplayLimitRequest request,
            [FromServices] IPromotionService promotionService,
            [FromServices] IMobilePortalSchemaService schemaService)
        {
            if (request.DisplayLimit < 1 || request.DisplayLimit > 50)
            {
                return BadRequest(new { success = false, message = "Display limit must be between 1 and 50" });
            }

            try
            {
                await schemaService.EnsurePromotionSchemaAsync();
                await promotionService.SetDisplayLimitAsync(request.DisplayLimit);
                return Ok(new
                {
                    success = true,
                    message = "Display limit updated",
                    displayLimit = request.DisplayLimit,
                });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        /// <summary>Create a launch promotion with image upload.</summary>
        [HttpPost("promotions")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(8_000_000)]
        public async Task<IActionResult> CreatePromotion(
            [FromForm] PromotionMultipartForm form,
            IFormFile? image,
            [FromServices] IPromotionService promotionService,
            [FromServices] IWebHostEnvironment env,
            [FromServices] IMobilePortalSchemaService schemaService)
        {
            if (string.IsNullOrWhiteSpace(form.Title))
            {
                return BadRequest(new { success = false, message = "Title is required" });
            }

            if (image == null || image.Length == 0)
            {
                return BadRequest(new { success = false, message = "Promotion image is required" });
            }

            try
            {
                await schemaService.EnsurePromotionSchemaAsync();
                var imageUrl = await SavePromotionImageAsync(image, env);
                var created = await promotionService.CreateAsync(new MobilePromotionRecord
                {
                    Title = form.Title.Trim(),
                    ImageUrl = imageUrl,
                    SortOrder = form.SortOrder,
                    DurationSeconds = form.DurationSeconds <= 0 ? 5 : form.DurationSeconds,
                    IsActive = form.ParseIsActive(),
                    StartAt = form.StartAt,
                    EndAt = form.EndAt,
                });

                return Ok(new
                {
                    success = true,
                    message = "Promotion created",
                    data = MapPromotionAdmin(created),
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "IIS cannot write to wwwroot/uploads/promotions. Grant Modify to IIS_IUSRS and the app pool identity.",
                    detail = ex.Message,
                });
            }
            catch (IOException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Could not write promotion image file. Check uploads folder exists and has Modify permission for the IIS app pool.",
                    detail = ex.Message,
                });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Could not save promotion image or database record. Check API logs and uploads folder permissions.",
                    detail = ex.Message,
                });
            }
        }

        /// <summary>Update promotion metadata; optional new image.</summary>
        [HttpPut("promotions/{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(8_000_000)]
        public async Task<IActionResult> UpdatePromotion(
            int id,
            [FromForm] PromotionMultipartForm form,
            IFormFile? image,
            [FromServices] IPromotionService promotionService,
            [FromServices] IWebHostEnvironment env,
            [FromServices] IMobilePortalSchemaService schemaService)
        {
            if (string.IsNullOrWhiteSpace(form.Title))
            {
                return BadRequest(new { success = false, message = "Title is required" });
            }

            try
            {
                await schemaService.EnsurePromotionSchemaAsync();
                var existing = await promotionService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new { success = false, message = "Promotion not found" });
                }

                if (image != null && image.Length > 0)
                {
                    existing.ImageUrl = await SavePromotionImageAsync(image, env);
                }

                existing.Title = form.Title.Trim();
                existing.SortOrder = form.SortOrder;
                existing.DurationSeconds = form.DurationSeconds <= 0 ? 5 : form.DurationSeconds;
                existing.IsActive = form.ParseIsActive();
                existing.StartAt = form.StartAt;
                existing.EndAt = form.EndAt;

                var updated = await promotionService.UpdateAsync(existing);
                if (!updated)
                {
                    return NotFound(new { success = false, message = "Promotion not found" });
                }

                var refreshed = await promotionService.GetByIdAsync(id);
                return Ok(new
                {
                    success = true,
                    message = "Promotion updated",
                    data = refreshed == null ? null : MapPromotionAdmin(refreshed),
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "IIS cannot write to wwwroot/uploads/promotions. Grant Modify to IIS_IUSRS and the app pool identity.",
                    detail = ex.Message,
                });
            }
            catch (IOException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Could not write promotion image file. Check uploads folder exists and has Modify permission for the IIS app pool.",
                    detail = ex.Message,
                });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Could not update promotion. Check API logs and uploads folder permissions.",
                    detail = ex.Message,
                });
            }
        }

        /// <summary>Delete a promotion.</summary>
        [HttpDelete("promotions/{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> DeletePromotion(
            int id,
            [FromServices] IPromotionService promotionService)
        {
            try
            {
                var deleted = await promotionService.DeleteAsync(id);
                return deleted
                    ? Ok(new { success = true, message = "Promotion deleted" })
                    : NotFound(new { success = false, message = "Promotion not found" });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        /// <summary>Help &amp; Support contact details shown in the mobile app.</summary>
        [HttpGet("support/contact")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> GetSupportContact([FromServices] ISupportService supportService)
        {
            var contact = await supportService.GetContactInfoAsync();
            return Ok(new { success = true, data = contact });
        }

        /// <summary>Update Call Us / Email Us / Visit Us / Operating Hours.</summary>
        [HttpPut("support/contact")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> UpdateSupportContact(
            [FromBody] SupportContactDto request,
            [FromServices] ISupportService supportService)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body is required" });
            }

            try
            {
                await supportService.UpdateContactInfoAsync(request);
                var refreshed = await supportService.GetContactInfoAsync();
                return Ok(new { success = true, message = "Support contact updated", data = refreshed });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        /// <summary>List all FAQ entries (including inactive) for admin editing.</summary>
        [HttpGet("support/faq")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> GetSupportFaqs([FromServices] ISupportService supportService)
        {
            var items = await supportService.GetAllFaqsAdminAsync();
            return Ok(new { success = true, data = items });
        }

        [HttpPost("support/faq")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> CreateSupportFaq(
            [FromBody] FaqAdminItem request,
            [FromServices] ISupportService supportService)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body is required" });
            }

            try
            {
                var id = await supportService.CreateFaqAsync(request);
                var created = await supportService.GetFaqByIdAsync(id);
                return Ok(new { success = true, message = "FAQ created", data = created });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        [HttpPut("support/faq/{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> UpdateSupportFaq(
            int id,
            [FromBody] FaqAdminItem request,
            [FromServices] ISupportService supportService)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body is required" });
            }

            request.FaqId = id;
            try
            {
                var updated = await supportService.UpdateFaqAsync(request);
                if (!updated)
                {
                    return NotFound(new { success = false, message = "FAQ not found" });
                }

                var refreshed = await supportService.GetFaqByIdAsync(id);
                return Ok(new { success = true, message = "FAQ updated", data = refreshed });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        [HttpDelete("support/faq/{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
        public async Task<IActionResult> DeleteSupportFaq(
            int id,
            [FromServices] ISupportService supportService)
        {
            var deleted = await supportService.DeleteFaqAsync(id);
            return deleted
                ? Ok(new { success = true, message = "FAQ deleted" })
                : NotFound(new { success = false, message = "FAQ not found" });
        }

        private static object MapPromotionAdmin(MobilePromotionRecord p) => new
        {
            promotionId = p.PromotionId,
            title = p.Title,
            imageUrl = p.ImageUrl,
            sortOrder = p.SortOrder,
            durationSeconds = p.DurationSeconds,
            isActive = p.IsActive,
            startAt = p.StartAt,
            endAt = p.EndAt,
            createdAt = p.CreatedAt,
            updatedAt = p.UpdatedAt,
        };

        private static async Task<string> SavePromotionImageAsync(IFormFile image, IWebHostEnvironment env)
        {
            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!allowed.Contains(ext))
            {
                throw new InvalidOperationException("Image must be JPG, PNG, WEBP, or GIF");
            }

            if (image.Length > 6_000_000)
            {
                throw new InvalidOperationException("Image must be 6 MB or smaller");
            }

            var webRoot = env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var folder = Path.Combine(webRoot, "uploads", "promotions");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await image.CopyToAsync(stream);
            }

            return $"/uploads/promotions/{fileName}";
        }
    }
}
