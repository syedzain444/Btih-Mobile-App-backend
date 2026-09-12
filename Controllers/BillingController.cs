using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Patient billing overview, invoices, and payment history.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Billing")]
    public class BillingController : ControllerBase
    {
        private readonly IBillingService _billingService;

        public BillingController(IBillingService billingService)
        {
            _billingService = billingService;
        }

        /// <summary>Billing overview — totals and per-department summary.</summary>
        [HttpGet("overview/{mrNo}")]
        public async Task<IActionResult> GetOverview(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            try
            {
                var overview = await _billingService.GetOverviewAsync(mrNo);
                return Ok(overview);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving billing overview",
                    error = ex.Message,
                });
            }
        }

        /// <summary>Payment history with optional filters.</summary>
        [HttpGet("history/{mrNo}")]
        public async Task<IActionResult> GetHistory(
            string mrNo,
            [FromQuery] string? department = null,
            [FromQuery] int? year = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? search = null,
            [FromQuery] string? paymentStatus = null)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            try
            {
                var history = await _billingService.GetHistoryAsync(
                    mrNo,
                    department,
                    year,
                    dateFrom,
                    dateTo,
                    search,
                    paymentStatus);

                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving billing history",
                    error = ex.Message,
                });
            }
        }

        /// <summary>Invoice details for a single bill.</summary>
        [HttpGet("invoices/{billId}")]
        public async Task<IActionResult> GetInvoice(string billId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (string.IsNullOrWhiteSpace(billId))
            {
                return BadRequest(new { success = false, message = "Bill ID is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            try
            {
                var invoice = await _billingService.GetInvoiceAsync(mrNo, billId);
                if (invoice == null)
                {
                    return NotFound(new { success = false, message = "Invoice not found" });
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving invoice details",
                    error = ex.Message,
                });
            }
        }

        /// <summary>Payment screen summary — pending balances and recent payments.</summary>
        [HttpGet("payment-summary/{mrNo}")]
        public async Task<IActionResult> GetPaymentSummary(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required" });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            try
            {
                var summary = await _billingService.GetPaymentSummaryAsync(mrNo);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving payment summary",
                    error = ex.Message,
                });
            }
        }
    }
}
