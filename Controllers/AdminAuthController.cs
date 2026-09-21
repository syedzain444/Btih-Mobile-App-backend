using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/admin/auth")]
    [AllowAnonymous]
    [Tags("Admin")]
    public class AdminAuthController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminAuthController(IAdminService adminService) => _adminService = adminService;

        /// <summary>Admin / staff portal login.</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required." });
            }

            var (success, message, token, user) = await _adminService.LoginAsync(request);
            if (!success || token == null || user == null)
            {
                return Unauthorized(new { success = false, message });
            }

            return Ok(new
            {
                success = true,
                message,
                token = token.Token,
                tokenType = "Bearer",
                expiresAt = token.ExpiresAt,
                expiresInSeconds = token.ExpiresInSeconds,
                user,
            });
        }

        /// <summary>One-time bootstrap to initialize admin password (requires bootstrap secret).</summary>
        [HttpPost("bootstrap")]
        public async Task<IActionResult> Bootstrap([FromBody] AdminBootstrapRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BootstrapSecret) ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Bootstrap secret, username, and password are required." });
            }

            var (success, message) = await _adminService.BootstrapAsync(request);
            return success
                ? Ok(new { success = true, message })
                : BadRequest(new { success = false, message });
        }
    }
}
