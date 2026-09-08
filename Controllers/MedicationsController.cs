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
    [Tags("Medications")]
    public class MedicationsController : ControllerBase
    {
        private readonly IMedicationService _medicationService;

        public MedicationsController(IMedicationService medicationService)
        {
            _medicationService = medicationService;
        }

        [HttpGet("current/{mrNo}")]
        public async Task<IActionResult> GetCurrentMedications(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var medications = await _medicationService.GetCurrentMedicationsAsync(mrNo.Trim());

            return Ok(new
            {
                success = true,
                count = medications.Count,
                data = medications,
            });
        }

        [HttpGet("{medicationId:int}")]
        public async Task<IActionResult> GetMedicationDetail(int medicationId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var medication = await _medicationService.GetMedicationDetailAsync(mrNo.Trim(), medicationId);

            if (medication == null)
            {
                return NotFound(new { success = false, message = "Medication not found" });
            }

            return Ok(new { success = true, data = medication });
        }

        [HttpPost("refill")]
        public async Task<IActionResult> RequestRefill([FromBody] RefillRequestPayload request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || request.MedicationId <= 0)
            {
                return BadRequest(new { success = false, message = "MR number and medication ID are required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            var refill = await _medicationService.CreateRefillRequestAsync(request);

            if (refill == null)
            {
                return NotFound(new { success = false, message = "Medication not found" });
            }

            return Ok(new
            {
                success = true,
                message = "Refill request submitted",
                data = refill,
            });
        }

        [HttpGet("refill/{refillId:int}")]
        public async Task<IActionResult> GetRefillStatus(int refillId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var refill = await _medicationService.GetRefillRequestAsync(refillId, mrNo.Trim());

            if (refill == null)
            {
                return NotFound(new { success = false, message = "Refill request not found" });
            }

            return Ok(new { success = true, data = refill });
        }

        [HttpGet("refills/{mrNo}")]
        public async Task<IActionResult> GetRefillHistory(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var refills = await _medicationService.GetRefillRequestsAsync(mrNo.Trim());

            return Ok(new
            {
                success = true,
                count = refills.Count,
                data = refills,
            });
        }
    }
}
