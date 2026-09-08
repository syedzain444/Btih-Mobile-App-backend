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

            await _pushNotificationService.UnregisterDeviceTokenAsync(request);

            return Ok(new { message = "Device token unregistered successfully" });
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
    }
}
