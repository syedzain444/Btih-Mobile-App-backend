using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Doctor directory, OPD schedules, and specialization filters.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [Tags("Doctor")]
    public class DoctorController : Controller
    {
        private readonly IDoctorService _docService;

        public DoctorController(IDoctorService docService)
        {
            _docService = docService;
        }

        //[HttpGet]
        //public async Task<IActionResult> GetDoctors()
        //{
        //    var doctors = await _docService.GetDoctorsAsync();

        //    if (doctors == null || !doctors.Any())
        //        return NotFound(new { message = "No doctors found" });

        //    return Ok(doctors);
        //}

        [HttpGet]
        public async Task<IActionResult> GetDoctors(int pageNumber = 1, int pageSize = 10)
        {
            if (pageNumber <= 0 || pageSize <= 0)
                return BadRequest(new { message = "Invalid pagination parameters" });

            try
            {
                var (doctors, totalCount) = await _docService.GetDoctorsAsync(pageNumber, pageSize);

                if (doctors == null || doctors.Count == 0)
                {
                    return Ok(new
                    {
                        Data = Array.Empty<object>(),
                        Pagination = new
                        {
                            PageNumber = pageNumber,
                            PageSize = pageSize,
                            TotalRecords = totalCount,
                            TotalPages = totalCount <= 0
                                ? 0
                                : (int)Math.Ceiling((double)totalCount / pageSize),
                        },
                        message = totalCount == 0 ? "No doctors found" : "No doctors on this page",
                    });
                }

                return Ok(new
                {
                    Data = doctors,
                    Pagination = new
                    {
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalRecords = totalCount,
                        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    },
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Doctor directory temporarily unavailable",
                    detail = ex.Message,
                });
            }
        }

        [HttpGet("{doctorId}/schedule")]
        public async Task<IActionResult> GetDoctorsSched(int doctorId)
        {
            var schedules = await _docService.GetDoctorScheduleAsync(doctorId);

            if (schedules == null || !schedules.Any())
                return NotFound(new { message = "No schedules found for this doctor" });

            return Ok(schedules);
        }
        [HttpGet("specialization")]
        public async Task<IActionResult> GetDoctorSpecialization()
        {
            var doctors = await _docService.GetDoctorSpecialization();

            if (doctors == null || !doctors.Any())
                return NotFound(new { message = "No specialization found" });

            return Ok(doctors);
        }
    }
}
