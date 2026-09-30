using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Appointment confirmation PDF + verifiable check-in QR (REQ-2026-014).</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("AppointmentConfirmation")]
    public class AppointmentConfirmationController : ControllerBase
    {
        private readonly IAppointmentConfirmationService _confirmationService;

        public AppointmentConfirmationController(IAppointmentConfirmationService confirmationService)
        {
            _confirmationService = confirmationService;
        }

        /// <summary>Download appointment confirmation PDF (details + scannable QR).</summary>
        [HttpGet("{appointmentId}/pdf")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadPdf(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId))
            {
                return BadRequest(new { success = false, message = "Appointment ID is required." });
            }

            try
            {
                var pdf = await _confirmationService.GenerateConfirmationPdfAsync(appointmentId.Trim());
                var fileName = $"Appointment_{appointmentId.Trim()}.pdf";
                return File(pdf, "application/pdf", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>Ensure / refresh confirmation QR for an appointment (returns token + payload + snapshot).</summary>
        [HttpPost("{appointmentId}/qr")]
        [AllowAnonymous]
        public async Task<IActionResult> EnsureQr(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId))
            {
                return BadRequest(new { success = false, message = "Appointment ID is required." });
            }

            try
            {
                var qr = await _confirmationService.EnsureQrAsync(appointmentId.Trim());
                return Ok(new { success = true, data = qr });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Hospital check-in: resolve QR token/payload to verified appointment details.
        /// Browsers / camera scanners receive a branded HTML page; pass ?format=json for API clients.
        /// </summary>
        [HttpGet("qr/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> ResolveQr(string token, [FromQuery] string? format = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                if (WantsHtml(format))
                {
                    return Html(
                        AppointmentConfirmationHtml.BuildErrorPage(
                            "Invalid QR",
                            "This confirmation link is missing a token. Ask reception for a new QR."),
                        StatusCodes.Status400BadRequest);
                }

                return BadRequest(new { success = false, message = "QR token is required." });
            }

            var resolved = await _confirmationService.ResolveQrAsync(token);
            if (resolved == null)
            {
                if (WantsHtml(format))
                {
                    return Html(
                        AppointmentConfirmationHtml.BuildErrorPage(
                            "QR not found",
                            "This appointment confirmation QR is invalid or no longer active."),
                        StatusCodes.Status404NotFound);
                }

                return NotFound(new { success = false, message = "Appointment QR not found or invalid." });
            }

            if (WantsHtml(format))
            {
                return Html(AppointmentConfirmationHtml.BuildVerifiedPage(resolved));
            }

            return Ok(new
            {
                success = true,
                message = "Appointment verified from confirmation QR.",
                data = resolved,
            });
        }

        private bool WantsHtml(string? format)
        {
            if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var accept = Request.Headers.Accept.ToString();
            if (string.IsNullOrWhiteSpace(accept) || accept.Contains("*/*", StringComparison.Ordinal))
            {
                // Camera / QR apps often send */* — show the page.
                return true;
            }

            var prefersJson = accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
            return !prefersJson;
        }

        private ContentResult Html(string html, int statusCode = StatusCodes.Status200OK) =>
            new()
            {
                Content = html,
                ContentType = "text/html; charset=utf-8",
                StatusCode = statusCode,
            };
    }
}
