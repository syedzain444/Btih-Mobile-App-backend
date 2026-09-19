using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>FCM push notification registration and hospital-triggered sends.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("PushNotification")]
    public class PushNotificationController : ControllerBase
    {
        private readonly IPushNotificationService _pushNotificationService;

        public PushNotificationController(IPushNotificationService pushNotificationService)
        {
            _pushNotificationService = pushNotificationService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || string.IsNullOrWhiteSpace(request.DeviceToken))
            {
                return BadRequest(new { message = "MR No and device token are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _pushNotificationService.RegisterDeviceTokenAsync(request);

            return Ok(new { message = "Device token registered successfully" });
        }

        [HttpPost("unregister")]
        public async Task<IActionResult> UnregisterDeviceToken([FromBody] UnregisterDeviceTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || string.IsNullOrWhiteSpace(request.DeviceToken))
            {
                return BadRequest(new { message = "MR No and device token are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _pushNotificationService.UnregisterDeviceTokenAsync(request);

            return Ok(new { message = "Device token unregistered successfully" });
        }

        [HttpGet("devices")]
        public async Task<IActionResult> GetRegisteredDevices([FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { message = "MR No is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var devices = await _pushNotificationService.GetRegisteredDevicesAsync(mrNo);

            return Ok(new
            {
                data = devices.Select(device => new
                {
                    deviceToken = device.DeviceToken,
                    platform = device.Platform,
                    updatedAt = device.UpdatedAt,
                }),
            });
        }

        [HttpPost("unregister-all")]
        public async Task<IActionResult> UnregisterAllDeviceTokens(
            [FromBody] UnregisterAllDeviceTokensRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { message = "MR No is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            await _pushNotificationService.UnregisterAllDeviceTokensAsync(request.MrNo);

            return Ok(new { message = "All device tokens unregistered successfully" });
        }

        [HttpPost("appointment-reminder")]
        public async Task<IActionResult> SendAppointmentReminder([FromBody] SendPatientNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { message = "MR No is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                request.Title = "Appointment Reminder";
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                request.Body = "You have an upcoming appointment at Bahria Town International Hospital.";
            }

            var result = await _pushNotificationService.SendAppointmentReminderAsync(request);

            return Ok(new
            {
                message = "Appointment reminder processed",
                result,
            });
        }

        [HttpPost("report-ready")]
        public async Task<IActionResult> SendReportReadyAlert([FromBody] SendPatientNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { message = "MR No is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                request.Title = "Report Ready";
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                request.Body = "Your medical report is now available in the patient portal.";
            }

            var result = await _pushNotificationService.SendReportReadyNotificationAsync(request);

            return Ok(new
            {
                message = "Report-ready alert processed",
                result,
            });
        }

        /// <summary>
        /// Send any typed patient notification (saved to PATIENT_NOTIFICATION with category + createdAt).
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendTypedNotification([FromBody] SendTypedNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) ||
                string.IsNullOrWhiteSpace(request.NotificationType) ||
                string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "MR No, notificationType, title, and body are required",
                });
            }

            var type = request.NotificationType.Trim().ToLowerInvariant().Replace('-', '_');
            if (!PushNotificationTypes.All.Contains(type))
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Unknown notificationType '{request.NotificationType}'. See PushNotificationTypes catalog.",
                    allowedTypes = PushNotificationTypes.All.OrderBy(x => x),
                });
            }

            var data = request.Payload != null
                ? new Dictionary<string, string>(request.Payload)
                : new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                data["category"] = request.Category.Trim().ToLowerInvariant();
            }

            if (!string.IsNullOrWhiteSpace(request.Priority))
            {
                data["priority"] = request.Priority.Trim().ToLowerInvariant();
            }

            var result = await _pushNotificationService.SendToPatientAsync(
                request.MrNo.Trim(),
                request.Title.Trim(),
                request.Body.Trim(),
                type,
                data);

            return Ok(new
            {
                success = true,
                message = "Notification processed",
                notificationType = type,
                category = NotificationHistoryService.ResolveCategory(type, data),
                result,
            });
        }
    }
}
