using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService) => _paymentService = paymentService;

        /// <summary>Create a payment intent and checkout URL for an invoice.</summary>
        [HttpPost("initiate")]
        [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
        public async Task<IActionResult> Initiate([FromBody] CreatePaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo) || request.Amount <= 0)
            {
                return BadRequest(new { success = false, message = "MR number and valid amount are required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, request.MrNo))
            {
                return Forbid();
            }

            try
            {
                var intent = await _paymentService.CreateIntentAsync(request);
                return Ok(new { success = true, data = intent });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>Confirm payment after gateway redirect or mock testing.</summary>
        [HttpPost("confirm")]
        [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
        public async Task<IActionResult> Confirm([FromBody] ConfirmPaymentRequest request)
        {
            try
            {
                var intent = await _paymentService.ConfirmAsync(request);
                if (intent == null)
                {
                    return NotFound(new { success = false, message = "Payment not found." });
                }

                return Ok(new { success = true, data = intent });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>Payment gateway webhook (no JWT — verified by signature).</summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();
            var signature = Request.Headers["X-Payment-Signature"].FirstOrDefault();

            var ok = await _paymentService.ProcessWebhookAsync(signature, rawBody);
            return ok ? Ok(new { success = true }) : Unauthorized(new { success = false });
        }

        /// <summary>Get payment intent status.</summary>
        [HttpGet("{paymentId:int}")]
        [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
        public async Task<IActionResult> GetPayment(int paymentId, [FromQuery] string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var intent = await _paymentService.GetIntentAsync(paymentId, mrNo.Trim());
            return intent == null
                ? NotFound(new { success = false, message = "Payment not found." })
                : Ok(new { success = true, data = intent });
        }

        /// <summary>Online payment history for a patient.</summary>
        [HttpGet("history/{mrNo}")]
        [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
        public async Task<IActionResult> GetHistory(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { success = false, message = "MR number is required." });
            }

            if (!PatientAuthorizationHelper.IsAuthorizedForMrNo(User, mrNo))
            {
                return Forbid();
            }

            var history = await _paymentService.GetHistoryAsync(mrNo.Trim());
            return Ok(new { success = true, data = history });
        }
    }
}
