using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("MedicationReminders")]
    public class MedicationRemindersController : ControllerBase
    {
        private readonly IReminderService _reminderService;

        public MedicationRemindersController(IReminderService reminderService)
        {
            _reminderService = reminderService;
        }

        [HttpGet("{mrNo}")]
        public async Task<IActionResult> GetReminders(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var reminders = await _reminderService.GetRemindersAsync(mrNo.Trim());
            return Ok(new { success = true, count = reminders.Count, data = reminders });
        }

        [HttpPost]
        public async Task<IActionResult> CreateReminder([FromBody] MedicationReminderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo)
                || string.IsNullOrWhiteSpace(request.MedicationName)
                || string.IsNullOrWhiteSpace(request.ReminderTime))
            {
                return BadRequest(new { success = false, message = "MR number, medication name, and reminder time are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var reminder = await _reminderService.CreateReminderAsync(request);

            if (reminder == null)
            {
                return BadRequest(new { success = false, message = "Invalid reminder time. Use HH:mm format (e.g. 09:30)" });
            }

            return Ok(new
            {
                success = true,
                message = "Reminder created",
                data = reminder,
            });
        }

        [HttpPut("{reminderId:int}")]
        public async Task<IActionResult> UpdateReminder(int reminderId, [FromBody] UpdateMedicationReminderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var updated = await _reminderService.UpdateReminderAsync(reminderId, request);

            if (!updated)
            {
                return NotFound(new { success = false, message = "Reminder not found or invalid reminder time" });
            }

            var reminders = await _reminderService.GetRemindersAsync(request.MrNo.Trim());
            var reminder = reminders.FirstOrDefault(r => r.ReminderId == reminderId);

            return Ok(new
            {
                success = true,
                message = "Reminder updated",
                data = reminder,
            });
        }

        [HttpDelete("{reminderId:int}")]
        public async Task<IActionResult> DeleteReminder(int reminderId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var deleted = await _reminderService.DeleteReminderAsync(reminderId, mrNo.Trim());

            if (!deleted)
            {
                return NotFound(new { success = false, message = "Reminder not found" });
            }

            return Ok(new { success = true, message = "Reminder deleted" });
        }
    }
}
