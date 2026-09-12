using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
using HospitalMobileAPPApi.Services;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;



namespace HospitalMobileAPPApi.Controllers

{

    [ApiController]

    [Route("api/[controller]")]

    [AllowAnonymous]

    [Tags("Guest")]

    public class GuestController : ControllerBase

    {

        private readonly IGuestService _guestService;

        private readonly IAuthService _authService;

        private readonly ILogger<GuestController> _logger;



        public GuestController(

            IGuestService guestService,

            IAuthService authService,

            ILogger<GuestController> logger)

        {

            _guestService = guestService;

            _authService = authService;

            _logger = logger;

        }



        [HttpPost("profile")]

        public async Task<IActionResult> SaveProfile([FromBody] GuestProfileRequest request)

        {

            if (request == null ||

                string.IsNullOrWhiteSpace(request.FullName) ||

                string.IsNullOrWhiteSpace(request.MobileNumber) ||

                string.IsNullOrWhiteSpace(request.Gender))

            {

                return BadRequest(new

                {

                    success = false,

                    message = "Full name, mobile number, gender, and date of birth are required",

                });

            }



            if (request.DateOfBirth == default)

            {

                return BadRequest(new

                {

                    success = false,

                    message = "Date of birth is required",

                });

            }



            try

            {

                var mobile = GuestRepository.NormalizeMobile(request.MobileNumber);

                var existingMr = await _authService.GetMrNoByContactNoAsync(mobile);

                if (!string.IsNullOrWhiteSpace(existingMr))

                {

                    return Conflict(new

                    {

                        success = false,

                        message = "This mobile number is already linked to a patient account. Please log in.",

                        mrNo = existingMr,

                    });

                }



                var profile = await _guestService.SaveProfileAsync(new GuestProfileRequest

                {

                    FullName = request.FullName.Trim(),

                    MobileNumber = mobile,

                    DateOfBirth = request.DateOfBirth.Date,

                    Gender = request.Gender.Trim(),

                });



                return Ok(new

                {

                    success = true,

                    message = "Guest profile saved",

                    guestId = profile.GuestId,

                    fullName = profile.FullName,

                    mobileNumber = profile.MobileNumber,

                    dateOfBirth = profile.DateOfBirth.ToString("yyyy-MM-dd"),

                    gender = profile.Gender,

                });

            }

            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))

            {

                _logger.LogError(ex, "Failed to save guest profile");

                return StatusCode(statusCode, new { success = false, message = dbMessage });

            }

        }



        [HttpGet("profile")]

        public async Task<IActionResult> GetProfile([FromQuery] string mobileNumber)

        {

            if (string.IsNullOrWhiteSpace(mobileNumber))

            {

                return BadRequest(new { success = false, message = "Mobile number is required" });

            }



            try

            {

                var profile = await _guestService.GetProfileByMobileAsync(mobileNumber);

                if (profile == null)

                {

                    return NotFound(new { success = false, message = "Guest profile not found" });

                }



                return Ok(new

                {

                    success = true,

                    guestId = profile.GuestId,

                    fullName = profile.FullName,

                    mobileNumber = profile.MobileNumber,

                    dateOfBirth = profile.DateOfBirth.ToString("yyyy-MM-dd"),

                    gender = profile.Gender,

                });

            }

            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))

            {

                _logger.LogError(ex, "Failed to load guest profile");

                return StatusCode(statusCode, new { success = false, message = dbMessage });

            }

        }



        [HttpGet("appointments")]

        public async Task<IActionResult> GetAppointments([FromQuery] string phoneNumber)

        {

            if (string.IsNullOrWhiteSpace(phoneNumber))

            {

                return BadRequest(new { success = false, message = "Phone number is required" });

            }



            try

            {

                var profile = await _guestService.GetProfileByMobileAsync(phoneNumber);

                if (profile == null)

                {

                    return NotFound(new

                    {

                        success = false,

                        message = "Complete your guest profile before viewing appointments",

                    });

                }



                var appointments = await _guestService.GetAppointmentsByPhoneAsync(phoneNumber);

                return Ok(appointments);

            }

            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))

            {

                _logger.LogError(ex, "Failed to load guest appointments");

                return StatusCode(statusCode, new { success = false, message = dbMessage });

            }

        }



        [HttpPut("appointments/{appointmentId}/cancel")]

        public async Task<IActionResult> CancelAppointment(

            string appointmentId,

            [FromBody] GuestCancelAppointmentRequest request)

        {

            if (request == null ||

                string.IsNullOrWhiteSpace(request.PhoneNumber) ||

                string.IsNullOrWhiteSpace(request.Reason))

            {

                return BadRequest(new { success = false, message = "Phone number and reason are required" });

            }



            if (request.Reason.Trim().Length < 5)

            {

                return BadRequest(new

                {

                    success = false,

                    message = "Please provide a meaningful cancellation reason (at least 5 characters)",

                });

            }



            var cancelled = await _guestService.CancelAppointmentAsync(

                appointmentId,

                request.PhoneNumber,

                request.Reason);



            if (!cancelled)

            {

                return NotFound(new

                {

                    success = false,

                    message = "Appointment not found or cannot be cancelled",

                });

            }



            return Ok(new

            {

                success = true,

                message = "Appointment cancelled successfully",

                appointmentId,

                status = "Cancelled",

            });

        }

    }

}

