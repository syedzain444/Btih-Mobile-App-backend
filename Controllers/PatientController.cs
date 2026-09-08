using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Patient profile, health records, appointments, and discharge history.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Patient")]
    public class PatientController : Controller
    {
        private const string PasswordResetCachePrefix = "pwd_reset_verified:";

        private readonly IPatientService _patService;
        private readonly IMemoryCache _cache;

        public PatientController(IPatientService patService, IMemoryCache cache)
        {
            _patService = patService;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> GetPatient(
            string MR_NO,
            int visitPageNumber = 1,
            int visitPageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(MR_NO))
            {
                return BadRequest(new { message = "MR number is required" });
            }

            if (visitPageNumber < 1)
            {
                visitPageNumber = 1;
            }

            if (visitPageSize < 1)
            {
                visitPageSize = 20;
            }

            if (visitPageSize > 100)
            {
                visitPageSize = 100;
            }

            var patient = await _patService.GetPatientProfileAsync(MR_NO, visitPageNumber, visitPageSize);

            if (patient == null)
            {
                return NotFound(new { message = "No patient found" });
            }

            return Ok(new
            {
                profile = patient.Profile,
                visitHistory = patient.VisitHistory.Data,
                visitHistoryPagination = new
                {
                    pageNumber = patient.VisitHistory.PageNumber,
                    pageSize = patient.VisitHistory.PageSize,
                    totalRecords = patient.VisitHistory.TotalRecords,
                    totalPages = patient.VisitHistory.TotalPages,
                },
            });
        }

        [HttpGet("{MR_NO}/labReports")]
        public async Task<IActionResult> GetLaboratoryReports(string MR_NO)
        {
            var labReports = await _patService.GetPatientReports(MR_NO);

            if (labReports == null || !labReports.Any())
            {
                return NotFound(new { message = "No reports found" });
            }

            return Ok(labReports);
        }

        [HttpGet("{MR_NO}/gastroReports")]
        public async Task<IActionResult> GetGastroReports(string MR_NO)
        {
            var gastroReports = await _patService.GetGastroReports(MR_NO);

            if (gastroReports == null || !gastroReports.Any())
            {
                return NotFound(new { message = "No reports found" });
            }

            return Ok(gastroReports);
        }

        [HttpGet("{MR_NO}/radiologyReports")]
        public async Task<IActionResult> GetRadiology(string MR_NO)
        {
            var radiologyReports = await _patService.GetRadiology(MR_NO);

            if (radiologyReports == null || !radiologyReports.Any())
            {
                return NotFound(new { message = "No reports found" });
            }

            return Ok(radiologyReports);
        }

        [HttpGet("GetReportFromWebsite/{id}")]
        public async Task<IActionResult> GetReportFromWebsite(int id)
        {
            var url = $"https://btkhospital.com/patientreports/Reports/PDF/{id}.pdf";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest("Cannot fetch report");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return File(bytes, "application/pdf");
        }

        [HttpGet("{MR_NO}/prescriptionReports")]
        public async Task<IActionResult> GetPrescriptions(string MR_NO)
        {
            var prescriptions = await _patService.GetPrescriptions(MR_NO);

            if (prescriptions == null || !prescriptions.Any())
            {
                return NotFound(new { message = "No prescriptions found" });
            }

            return Ok(prescriptions);
        }

        [AllowAnonymous]
        [HttpPost("insertchallan")]
        public async Task<IActionResult> InsertAppointment([FromBody] AppointmentModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(model.name))
            {
                return BadRequest(new { message = "Patient name is required" });
            }

            if (string.IsNullOrWhiteSpace(model.phoneNo))
            {
                return BadRequest(new { message = "Phone number is required" });
            }

            var rowsAffected = await _patService.InsertAppointment(model);

            if (rowsAffected == 0)
            {
                return BadRequest(new { message = "Insertion failed" });
            }

            return Ok(new
            {
                message = "Appointment requested successfully",
                rowsAffected,
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdatePatientProfileRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MrNo))
            {
                return BadRequest(new { message = "MR No is required" });
            }

            var updated = await _patService.UpdatePatientProfileAsync(request);

            if (!updated)
            {
                return NotFound(new { message = "Patient not found or profile update failed" });
            }

            var profile = await _patService.GetPatientProfileAsync(request.MrNo, 1, 20);

            return Ok(new
            {
                message = "Profile updated successfully",
                profile = profile?.Profile,
            });
        }

        [AllowAnonymous]
        [HttpPost("updatePassword")]
        public async Task<IActionResult> UpdatePassword(string mrno, string patientPassword)
        {
            if (string.IsNullOrWhiteSpace(mrno) || string.IsNullOrWhiteSpace(patientPassword))
            {
                return BadRequest(new { message = "MR number and new password are required" });
            }

            if (patientPassword.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters" });
            }

            var cacheKey = $"{PasswordResetCachePrefix}{mrno.Trim()}";
            if (!_cache.TryGetValue(cacheKey, out _))
            {
                return Unauthorized(new
                {
                    message = "Password reset is not authorized. Verify OTP first.",
                });
            }

            var result = await _patService.UpdatePatientPassword(mrno.Trim(), patientPassword);

            if (!result)
            {
                return BadRequest(new { message = "Password update failed" });
            }

            _cache.Remove(cacheKey);

            return Ok(new { message = "Password updated successfully" });
        }

        [HttpGet("appointments/{mrno}")]
        public async Task<IActionResult> GetAppointments(string mrno)
        {
            var appointment = await _patService.GetAppointments(mrno);

            if (appointment == null || !appointment.Any())
            {
                return NotFound(new { message = "No appointment found...." });
            }

            return Ok(appointment);
        }

        [HttpGet("dischargeHistory/{mrno}")]
        public async Task<IActionResult> GetDischargeHistory(string mrno, int pageNumber = 1, int pageSize = 10)
        {
            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var result = await _patService.GetDischargeHistoryAsync(mrno, pageNumber, pageSize);

            if (result.Data == null || !result.Data.Any())
            {
                return NotFound(new { message = "No discharge history found...." });
            }

            Response.Headers["X-Page-Number"] = result.PageNumber.ToString();
            Response.Headers["X-Page-Size"] = result.PageSize.ToString();
            Response.Headers["X-Total-Count"] = result.TotalRecords.ToString();

            return Ok(new
            {
                pageNumber = result.PageNumber,
                pageSize = result.PageSize,
                totalRecords = result.TotalRecords,
                totalPages = result.TotalPages,
                data = result.Data,
            });
        }
    }
}
