using System.Text.Json;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Patient notification inbox — persisted FCM history.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Notification")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationHistoryService _notificationHistoryService;

        public NotificationController(INotificationHistoryService notificationHistoryService)
        {
            _notificationHistoryService = notificationHistoryService;
        }

        /// <summary>Get paginated notification history for a patient.</summary>
        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox(
            [FromQuery] string mrNo,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string category = "all")
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

            var inbox = await _notificationHistoryService.GetInboxAsync(
                mrNo.Trim(),
                pageNumber,
                pageSize,
                category);

            return Ok(new
            {
                success = true,
                count = inbox.Data.Count,
                unreadCount = inbox.UnreadCount,
                pagination = new
                {
                    pageNumber = inbox.PageNumber,
                    pageSize = inbox.PageSize,
                    totalRecords = inbox.TotalRecords,
                    totalPages = inbox.TotalPages,
                },
                data = inbox.Data.Select(MapNotification),
            });
        }

        /// <summary>Unread notification count for dashboard bell badge.</summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var unreadCount = await _notificationHistoryService.GetUnreadCountAsync(mrNo.Trim());

            return Ok(new
            {
                success = true,
                unreadCount,
            });
        }

        /// <summary>Mark a single notification as read.</summary>
        [HttpPatch("{notificationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(
            int notificationId,
            [FromBody] MarkNotificationReadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var updated = await _notificationHistoryService.MarkAsReadAsync(
                notificationId,
                request.MrNo.Trim());

            if (!updated)
            {
                return NotFound(new { success = false, message = "Notification not found" });
            }

            return Ok(new
            {
                success = true,
                message = "Notification marked as read",
            });
        }

        /// <summary>Mark all notifications as read for a patient.</summary>
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead([FromBody] MarkNotificationReadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var updatedCount = await _notificationHistoryService.MarkAllAsReadAsync(request.MrNo.Trim());

            return Ok(new
            {
                success = true,
                message = "All notifications marked as read",
                updatedCount,
            });
        }

        /// <summary>
        /// Record a notification in the inbox (optional client-side ingest after FCM delivery).
        /// </summary>
        [HttpPost("record")]
        public async Task<IActionResult> RecordNotification([FromBody] RecordNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) ||
                string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Body) ||
                string.IsNullOrWhiteSpace(request.NotificationType))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "MR number, notification type, title, and body are required",
                });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var notificationId = await _notificationHistoryService.RecordNotificationAsync(
                request.MrNo.Trim(),
                request.Title.Trim(),
                request.Body.Trim(),
                request.NotificationType.Trim(),
                request.Payload,
                request.Category,
                request.Priority);

            return Ok(new
            {
                success = true,
                message = "Notification recorded",
                notificationId,
            });
        }

        private static object MapNotification(PatientNotificationRecord record)
        {
            object? payload = null;
            if (!string.IsNullOrWhiteSpace(record.PayloadJson))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<Dictionary<string, string>>(record.PayloadJson);
                }
                catch
                {
                    payload = record.PayloadJson;
                }
            }

            return new
            {
                notificationId = record.NotificationId,
                id = record.NotificationId.ToString(),
                type = record.NotificationType,
                category = record.Category,
                priority = record.Priority,
                title = record.Title,
                body = record.Body,
                payload,
                isRead = record.IsRead,
                createdAt = record.CreatedAt,
                readAt = record.ReadAt,
            };
        }
    }
}
